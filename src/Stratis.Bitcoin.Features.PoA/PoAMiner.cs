using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

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
    /// of current key in <see cref="IFederationManager.GetFederationMembers"/>
    /// </remarks>
    public interface IPoAMiner : IDisposable
    {
        /// <summary>Starts mining loop.</summary>
        void InitializeMining();
    }

    /// <inheritdoc cref="IPoAMiner"/>
    public class PoAMiner : IPoAMiner
    {
        private readonly IConsensusManager consensusManager;

        private readonly IDateTimeProvider dateTimeProvider;

        protected readonly ILogger logger;

        protected readonly PoANetwork network;

        /// <summary>
        /// A cancellation token source that can cancel the mining processes and is linked to the <see cref="INodeLifetime.ApplicationStopping"/>.
        /// </summary>
        private CancellationTokenSource cancellation;

        private readonly IInitialBlockDownloadState ibdState;

        private readonly BlockDefinition blockDefinition;

        private readonly ISlotsManager slotsManager;

        private readonly IConnectionManager connectionManager;

        private readonly PoABlockHeaderValidator poaHeaderValidator;

        protected readonly IFederationManager federationManager;

        private readonly IIntegrityValidator integrityValidator;

        private readonly IWalletManager walletManager;

        private readonly VotingManager votingManager;

        private readonly VotingDataEncoder votingDataEncoder;

        private readonly PoAMinerSettings settings;
        private readonly IAsyncProvider asyncProvider;

        private Task miningTask;

        public PoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            BlockDefinition blockDefinition,
            ISlotsManager slotsManager,
            IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator,
            IFederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats,
            VotingManager votingManager,
            PoAMinerSettings poAMinerSettings,
            IAsyncProvider asyncProvider)
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
            this.votingManager = votingManager;
            this.settings = poAMinerSettings;
            this.asyncProvider = asyncProvider;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(new[] { nodeLifetime.ApplicationStopping });
            this.votingDataEncoder = new VotingDataEncoder(loggerFactory);

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name);
        }

        /// <inheritdoc />
        public virtual void InitializeMining()
        {
            if (this.miningTask == null)
            {
                this.miningTask = this.CreateBlocksAsync();
                this.asyncProvider.RegisterTask($"{nameof(PoAMiner)}.{nameof(this.miningTask)}", this.miningTask);
            }
        }

        private async Task CreateBlocksAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    this.logger.LogDebug("IsInitialBlockDownload={0}, AnyConnectedPeers={1}, BootstrappingMode={2}, IsFederationMember={3}",
                        this.ibdState.IsInitialBlockDownload(), this.connectionManager.ConnectedPeers.Any(), this.settings.BootstrappingMode, this.federationManager.IsFederationMember);

                    // Don't mine in IBD in case we are connected to any node unless bootstrapping mode is enabled.
                    if (((this.ibdState.IsInitialBlockDownload() || !this.connectionManager.ConnectedPeers.Any()) && !this.settings.BootstrappingMode)
                        || !this.federationManager.IsFederationMember)
                    {
                        int attemptDelayMs = 30_000;
                        await Task.Delay(attemptDelayMs, this.cancellation.Token).ConfigureAwait(false);

                        continue;
                    }

                    uint miningTimestamp = await this.WaitUntilMiningSlotAsync().ConfigureAwait(false);

                    ChainedHeader chainedHeader = await this.MineBlockAtTimestampAsync(miningTimestamp).ConfigureAwait(false);

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
                catch (ConsensusErrorException ce)
                {
                    // Text from PosMinting:
                    // All consensus exceptions should be ignored. It means that the miner
                    // ran into problems while constructing block or verifying it
                    // but it should not halt the mining operation.
                    this.logger.LogWarning("Miner failed to mine block due to: '{0}'.", ce.ConsensusError.Message);
                }
                catch (Exception exception)
                {
                    this.logger.LogCritical("Exception occurred during mining: {0}", exception.ToString());
                    break;
                }
            }
        }

        private async Task<uint> WaitUntilMiningSlotAsync()
        {
            uint? myTimestamp = null;

            while (!this.cancellation.IsCancellationRequested)
            {
                uint timeNow = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();

                if (timeNow <= this.consensusManager.Tip.Header.Time)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                    continue;
                }

                if (myTimestamp == null)
                {
                    try
                    {
                        myTimestamp = this.slotsManager.GetMiningTimestamp(timeNow);
                    }
                    catch (NotAFederationMemberException)
                    {
                        this.logger.LogWarning("This node is no longer a federation member!");

                        throw new OperationCanceledException();
                    }
                }

                int estimatedWaitingTime = (int)(myTimestamp - timeNow) - 1;

                if (estimatedWaitingTime <= 0)
                    return myTimestamp.Value;

                await Task.Delay(TimeSpan.FromMilliseconds(500), this.cancellation.Token).ConfigureAwait(false);
            }

            throw new OperationCanceledException();
        }

        protected async Task<ChainedHeader> MineBlockAtTimestampAsync(uint timestamp)
        {
            ChainedHeader tip = this.consensusManager.Tip;

            // Timestamp should always be greater than prev one.
            if (timestamp <= tip.Header.Time)
            {
                // Can happen only when target spacing had crazy low value or key was compromised and someone is mining with our key.
                this.logger.LogWarning("Somehow another block was connected with greater timestamp. Dropping current block.");
                this.logger.LogTrace("(-)[ANOTHER_BLOCK_CONNECTED]:null");
                return null;
            }

            Script walletScriptPubKey = this.GetScriptPubKeyFromWallet();

            if (walletScriptPubKey == null)
            {
                this.logger.LogWarning("Miner wasn't able to get address from the wallet! You will not receive any rewards.");
                walletScriptPubKey = new Script();
            }

            BlockTemplate blockTemplate = this.blockDefinition.Build(tip, walletScriptPubKey);

            this.FillBlockTemplate(blockTemplate, out bool dropTemplate);

            if (dropTemplate)
            {
                this.logger.LogTrace("(-)[DROPPED]:null");
                return null;
            }

            blockTemplate.Block.Header.Time = timestamp;

            // Update merkle root.
            blockTemplate.Block.UpdateMerkleRoot();

            // Sign block with our private key.
            var header = blockTemplate.Block.Header as PoABlockHeader;
            this.poaHeaderValidator.Sign(this.federationManager.CurrentFederationKey, header);

            ChainedHeader chainedHeader = await this.consensusManager.BlockMinedAsync(blockTemplate.Block).ConfigureAwait(false);

            if (chainedHeader == null)
            {
                // Block wasn't accepted because we already connected block from the network.
                this.logger.LogTrace("(-)[FAILED_TO_CONNECT]:null");
                return null;
            }

            ValidationContext result = this.integrityValidator.VerifyBlockIntegrity(chainedHeader, blockTemplate.Block);
            if (result.Error != null)
            {
                // Sanity check. Should never happen.
                this.logger.LogTrace("(-)[INTEGRITY_FAILURE]");
                throw new Exception(result.Error.ToString());
            }

            return chainedHeader;
        }

        /// <summary>Fills block template with custom non-standard data.</summary>
        protected virtual void FillBlockTemplate(BlockTemplate blockTemplate, out bool dropTemplate)
        {
            if (this.network.ConsensusOptions.VotingEnabled)
                this.AddVotingData(blockTemplate);

            dropTemplate = false;
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

        /// <summary>Adds OP_RETURN output to a coinbase transaction which contains encoded voting data.</summary>
        /// <remarks>If there are no votes scheduled output will not be added.</remarks>
        private void AddVotingData(BlockTemplate blockTemplate)
        {
            List<VotingData> scheduledVotes = this.votingManager.GetAndCleanScheduledVotes();

            if (scheduledVotes.Count == 0)
            {
                this.logger.LogTrace("(-)[NO_DATA]");
                return;
            }

            var votingData = new List<byte>(VotingDataEncoder.VotingOutputPrefixBytes);

            byte[] encodedVotingData = this.votingDataEncoder.Encode(scheduledVotes);
            votingData.AddRange(encodedVotingData);

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(votingData.ToArray()));

            blockTemplate.Block.Transactions[0].AddOutput(Money.Zero, votingOutputScript);
        }

        [NoTrace]
        private void AddComponentStats(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("======PoA Miner======");

            ChainedHeader tip = this.consensusManager.Tip;
            ChainedHeader currentHeader = tip;
            uint currentTime = currentHeader.Header.Time;

            int maxDepth = 46;
            int pubKeyTakeCharacters = 4;
            int depthReached = 0;
            int hitCount = 0;

            log.AppendLine($"Mining information for the last {maxDepth} blocks.");
            log.AppendLine("MISS means that miner didn't produce a block at the timestamp he was supposed to.");

            for (int i = tip.Height; (i > 0) && (i > tip.Height - maxDepth); i--)
            {
                // Add stats for current header.
                string pubKeyRepresentation = this.slotsManager.GetFederationMemberForTimestamp(currentTime).PubKey.ToString().Substring(0, pubKeyTakeCharacters);

                log.Append("[" + pubKeyRepresentation + "]-");
                depthReached++;
                hitCount++;

                currentHeader = currentHeader.Previous;
                currentTime -= this.network.ConsensusOptions.TargetSpacingSeconds;

                if (currentHeader.Height == 0)
                    break;

                while ((currentHeader.Header.Time != currentTime) && (depthReached <= maxDepth))
                {
                    log.Append("MISS-");
                    currentTime -= this.network.ConsensusOptions.TargetSpacingSeconds;
                    depthReached++;
                }

                if (depthReached >= maxDepth)
                    break;
            }

            log.Append("...");
            log.AppendLine();
            log.AppendLine($"Block producers hits      : {hitCount} of {maxDepth}({(((float)hitCount / (float)maxDepth)).ToString("P2")})");
            log.AppendLine($"Block producers idle time : {TimeSpan.FromSeconds(this.network.ConsensusOptions.TargetSpacingSeconds * (maxDepth - hitCount)).ToString(@"hh\:mm\:ss")}");
            log.AppendLine();
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            this.cancellation.Cancel();
            this.miningTask?.GetAwaiter().GetResult();

            this.cancellation.Dispose();
        }
    }
}
