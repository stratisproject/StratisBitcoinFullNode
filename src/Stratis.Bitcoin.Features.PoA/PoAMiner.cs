using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <summary>
    /// Mines blocks for PoA network.
    /// Mining can happen only in case this node is a federation member.
    /// </summary>
    /// <remarks>
    /// Blocks can be created only for particular timestamps- once per round.
    /// Round length in seconds is equal to amount of fed members multiplied by target spacing.
    /// Miner's slot in each round is the same and is determined by the index
    /// of current key in <see cref="PoANetwork.FederationPublicKeys"/>
    /// </remarks>
    public interface IPoAMiner : IDisposable
    {
        /// <summary>Starts mining loop.</summary>
        void InitializeMining();

        bool IsMining();
    }

    /// <inheritdoc cref="IPoAMiner"/>
    public class PoAMiner : IPoAMiner
    {
        private readonly IConsensusManager consensusManager;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ILogger logger;

        private readonly PoANetwork network;

        /// <summary>
        /// A cancellation token source that can cancel the mining processes and is linked to the <see cref="INodeLifetime.ApplicationStopping"/>.
        /// </summary>
        private CancellationTokenSource cancellation;

        private readonly IInitialBlockDownloadState ibdState;

        private readonly BlockDefinition blockDefinition;

        private readonly SlotsManager slotsManager;

        private readonly IConnectionManager connectionManager;

        private readonly PoABlockHeaderValidator poaHeaderValidator;

        private readonly FederationManager federationManager;

        private readonly IIntegrityValidator integrityValidator;

        private readonly IWalletManager walletManager;

        private Task miningTask;

        public PoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            BlockDefinition blockDefinition,
            SlotsManager slotsManager,
            IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator,
            FederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats)
        {
            this.consensusManager = consensusManager;
            this.dateTimeProvider = dateTimeProvider;
            this.network = network as PoANetwork;
            this.ibdState = ibdState;
            this.blockDefinition = blockDefinition;
            this.slotsManager = slotsManager;
            this.connectionManager = connectionManager;
            this.poaHeaderValidator = poaHeaderValidator;
            this.federationManager = federationManager;
            this.integrityValidator = integrityValidator;
            this.walletManager = walletManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(new[] { nodeLifetime.ApplicationStopping });

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
        }

        /// <inheritdoc />
        public void InitializeMining()
        {
            if (this.miningTask == null)
            {
                this.miningTask = this.CreateBlocksAsync();
            }
        }

        public bool IsMining()
        {
            return this.miningTask != null;
        }

        private async Task CreateBlocksAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    // Don't mine in IBD in case we are connected to any node.
                    if (this.ibdState.IsInitialBlockDownload() && this.connectionManager.ConnectedPeers.Any())
                    {
                        int attemptDelayMs = 20_000;
                        await Task.Delay(attemptDelayMs, this.cancellation.Token).ConfigureAwait(false);

                        continue;
                    }

                    uint timeNow = (uint) this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();

                    if (timeNow <= this.consensusManager.Tip.Header.Time)
                    {
                        await this.TaskDelayAsync(500).ConfigureAwait(false);
                        continue;
                    }

                    uint myTimestamp = this.slotsManager.GetMiningTimestamp(timeNow);

                    await this.WaitUntilWeCanMineAsync(myTimestamp).ConfigureAwait(false);

                    ChainedHeader chainedHeader = await this.MineBlockAtTimestampAsync(myTimestamp).ConfigureAwait(false);

                    if (chainedHeader == null)
                    {
                        continue;
                    }

                    var builder = new StringBuilder();
                    builder.AppendLine("<<==============================================================>>");
                    builder.AppendLine($"Block was mined {chainedHeader}.");
                    builder.AppendLine("<<==============================================================>>");
                    this.logger.LogInformation(builder.ToString());
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    this.logger.LogCritical("Exception occurred during mining: {0}", exception.ToString());
                    break;
                }
            }
        }

        /// <summary>
        /// Doesn't let the node progress until the given time slot.
        /// </summary>
        /// <param name="myTimestamp">The next time slot this node should mine at.</param>
        protected virtual async Task WaitUntilWeCanMineAsync(uint myTimestamp)
        {
            int waitingTime = this.GetWaitingTimeInSeconds(myTimestamp);

            this.logger.LogInformation("Waiting {0} seconds until block can be mined.", waitingTime);

            // Accounts for different IDateTimeProvider implementations. The standard implementation would progress
            // after one iteration, but for other cases where we are emulating time, this may loop more than once.
            while (waitingTime > 0)
            {
                await this.TaskDelayAsync(waitingTime * 1000, this.cancellation.Token).ConfigureAwait(false);

                waitingTime = this.GetWaitingTimeInSeconds(myTimestamp);
            }
        }

        /// <summary>
        /// Retrieve the amount of seconds the node should wait until its turn to mine.
        /// </summary>
        /// <param name="myTimestamp">The next time slot this node should mine at.</param>
        private int GetWaitingTimeInSeconds(uint myTimestamp)
        {
            uint timeNow = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            return (int)(myTimestamp - timeNow) - 1;
        }

        /// <summary>
        /// Pauses execution for the given time. A wrapper for <see cref="Task.Delay(int)"/>.
        /// </summary>
        /// <param name="delayMs">Milliseconds to sleep for.</param>
        protected virtual async Task TaskDelayAsync(int delayMs, CancellationToken cancellation = default(CancellationToken))
        {
            await Task.Delay(delayMs, cancellation).ConfigureAwait(false);
        }

        protected async Task<ChainedHeader> MineBlockAtTimestampAsync(uint timestamp)
        {
            ChainedHeader tip = this.consensusManager.Tip;

            Script walletScriptPubKey = this.GetScriptPubKeyFromWallet();

            if (walletScriptPubKey == null)
            {
                this.logger.LogWarning("Miner wasn't able to get address from the wallet! You will not receive any rewards.");
                walletScriptPubKey = new Script();
            }

            BlockTemplate blockTemplate = this.blockDefinition.Build(tip, walletScriptPubKey);

            blockTemplate.Block.Header.Time = timestamp;

            // Timestamp should always be greater than prev one.
            if (blockTemplate.Block.Header.Time <= tip.Header.Time)
            {
                // Can happen only when target spacing had crazy low value or key was compromised and someone is mining with our key.
                this.logger.LogWarning("Somehow another block was connected with greater timestamp. Dropping current block.");
                return null;
            }

            // Update merkle root.
            blockTemplate.Block.UpdateMerkleRoot();

            // Sign block with our private key.
            var header = blockTemplate.Block.Header as PoABlockHeader;
            this.poaHeaderValidator.Sign(this.federationManager.FederationMemberKey, header);

            ChainedHeader chainedHeader = await this.consensusManager.BlockMinedAsync(blockTemplate.Block).ConfigureAwait(false);

            if (chainedHeader == null)
            {
                // Block wasn't accepted because we already connected block from the network.
                return null;
            }

            ValidationContext result = this.integrityValidator.VerifyBlockIntegrity(chainedHeader, blockTemplate.Block);
            if (result.Error != null)
            {
                // Sanity check. Should never happen.
                throw new Exception(result.Error.ToString());
            }

            return chainedHeader;
        }

        /// <summary>Gets scriptPubKey from the wallet.</summary>
        private Script GetScriptPubKeyFromWallet()
        {
            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();

            if (walletName == null)
                return null;

            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();

            if (account == null)
                return null;

            var walletAccountReference = new WalletAccountReference(walletName, account.Name);

            HdAddress address = this.walletManager.GetUnusedAddress(walletAccountReference);

            return address.Pubkey;
        }

        private void AddComponentStats(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("======PoA Miner======");

            ChainedHeader tip = this.consensusManager.Tip;
            ChainedHeader currentHeader = tip;
            uint currentTime = currentHeader.Header.Time;

            int depth = 20;
            int pubKeyTakeCharacters = 4;

            log.AppendLine($"Mining information for the last {depth} blocks.");
            log.AppendLine("MISS means that miner didn't produce a block at the timestamp he was supposed to.");

            for (int i = tip.Height; (i > 0) && (i > tip.Height - depth); i--)
            {
                // Add stats for current header.
                string pubKeyRepresentation = this.slotsManager.GetPubKeyForTimestamp(currentTime).ToString().Substring(0, pubKeyTakeCharacters);

                log.Append("[" + pubKeyRepresentation + "]-");

                currentHeader = currentHeader.Previous;
                currentTime -= this.network.ConsensusOptions.TargetSpacingSeconds;

                if (currentHeader.Height == 0)
                    break;

                while (currentHeader.Header.Time != currentTime)
                {
                    log.Append("MISS-");
                    currentTime -= this.network.ConsensusOptions.TargetSpacingSeconds;
                }
            }

            log.Append("...");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.cancellation.Cancel();
            this.miningTask?.GetAwaiter().GetResult();

            this.cancellation.Dispose();
        }
    }
}
