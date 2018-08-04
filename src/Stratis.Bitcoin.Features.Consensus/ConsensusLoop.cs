using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.IntegrationTests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.MemoryPool.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// Consumes incoming blocks, validates and executes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blocks are coming from <see cref="ILookaheadBlockPuller"/> or Miner/Staker and get validated by get validated by the <see cref="IConsensusRules" /> engine.
    /// See either the <see cref="FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration"/> for PoW or the <see cref="FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration"/> for PoS.
    /// </para>
    /// <para>
    /// When consensus loop is being initialized we rewind it in case block store is behind or the <see cref="Tip"/> is not part of the best chain.
    /// </para>
    /// </remarks>
    public class ConsensusLoop : IConsensusLoop, INetworkDifficulty, IGetUnspentTransaction
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Information holding POS data chained.</summary>
        public IStakeChain StakeChain { get; }

        /// <summary>A puller that can pull blocks from peers on demand.</summary>
        public LookaheadBlockPuller Puller { get; }

        /// <summary>A chain of headers all the way to genesis.</summary>
        public ConcurrentChain Chain { get; }

        /// <summary>The consensus db, containing all unspent UTXO in the chain.</summary>
        public ICoinView UTXOSet { get; }

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader Tip { get; private set; }

        /// <summary>Contain information about deployment and activation of features in the chain.</summary>
        public NodeDeployments NodeDeployments { get; private set; }

        /// <summary>Factory for creating and also possibly starting application defined tasks inside async loop.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Holds state related to the block chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Connection manager of all the currently connected peers.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>A signaler that used to signal messages between features.</summary>
        private readonly Signals.Signals signals;

        /// <summary>A lock object that synchronizes access to the <see cref="ConsensusLoop.AcceptBlockAsync"/> and the reorg part of <see cref="ConsensusLoop.PullerLoopAsync"/> methods.</summary>
        private readonly AsyncLock consensusLock;

        /// <summary>Settings for the full node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>Handles the banning of peers.</summary>
        private readonly IPeerBanning peerBanning;

        /// <summary>Consensus rules engine.</summary>
        public IConsensusRules ConsensusRules { get; }

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Specifies time threshold which is used to determine if flush is required.
        /// When consensus tip timestamp is greater than current time minus the threshold the flush is required.
        /// </summary>
        /// <remarks>Used only on blockchains without max reorg property.</remarks>
        private const int FlushRequiredThresholdSeconds = 2 * 24 * 60 * 60;

        /// <summary>
        /// Initialize a new instance of <see cref="ConsensusLoop"/>.
        /// </summary>
        /// <param name="asyncLoopFactory">The async loop we need to wait upon before we can shut down this feature.</param>
        /// <param name="nodeLifetime">Contain information about the life time of the node, its used on startup and shutdown.</param>
        /// <param name="chain">A chain of headers all the way to genesis.</param>
        /// <param name="utxoSet">The consensus db, containing all unspent UTXO in the chain.</param>
        /// <param name="puller">A puller that can pull blocks from peers on demand.</param>
        /// <param name="nodeDeployments">Contain information about deployment and activation of features in the chain.</param>
        /// <param name="loggerFactory">A factory to provide logger instances.</param>
        /// <param name="chainState">Holds state related to the block chain.</param>
        /// <param name="connectionManager">Connection manager of all the currently connected peers.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="signals">A signaler that used to signal messages between features.</param>
        /// <param name="consensusSettings">Consensus settings for the full node.</param>
        /// <param name="nodeSettings">Settings for the full node.</param>
        /// <param name="peerBanning">Handles the banning of peers.</param>
        /// <param name="consensusRules">The consensus rules to validate.</param>
        /// <param name="stakeChain">Information holding POS data chained.</param>
        public ConsensusLoop(
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime,
            ConcurrentChain chain,
            ICoinView utxoSet,
            LookaheadBlockPuller puller,
            NodeDeployments nodeDeployments,
            ILoggerFactory loggerFactory,
            IChainState chainState,
            IConnectionManager connectionManager,
            IDateTimeProvider dateTimeProvider,
            Signals.Signals signals,
            ConsensusSettings consensusSettings,
            NodeSettings nodeSettings,
            IPeerBanning peerBanning,
            IConsensusRules consensusRules,
            IStakeChain stakeChain = null)
        {
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(utxoSet, nameof(utxoSet));
            Guard.NotNull(puller, nameof(puller));
            Guard.NotNull(nodeDeployments, nameof(nodeDeployments));
            Guard.NotNull(connectionManager, nameof(connectionManager));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(consensusSettings, nameof(consensusSettings));
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            Guard.NotNull(peerBanning, nameof(peerBanning));
            Guard.NotNull(consensusRules, nameof(consensusRules));

            this.consensusLock = new AsyncLock();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.signals = signals;
            this.Chain = chain;
            this.UTXOSet = utxoSet;
            this.Puller = puller;
            this.NodeDeployments = nodeDeployments;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeSettings = nodeSettings;
            this.peerBanning = peerBanning;
            this.ConsensusRules = consensusRules;

            // chain of stake info can be null if POS is not enabled
            this.StakeChain = stakeChain;
        }

        /// <inheritdoc/>
        public async Task StartAsync()
        {
            this.logger.LogTrace("()");

            uint256 utxoHash = await this.UTXOSet.GetTipHashAsync().ConfigureAwait(false);
            bool blockStoreDisabled = this.chainState.BlockStoreTip == null;

            while (true)
            {
                this.Tip = this.Chain.GetBlock(utxoHash);

                if ((this.Tip != null) && (blockStoreDisabled || (this.chainState.BlockStoreTip.Height >= this.Tip.Height)))
                    break;

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                utxoHash = await this.UTXOSet.Rewind().ConfigureAwait(false);
            }

            this.Chain.SetTip(this.Tip);

            this.Puller.SetLocation(this.Tip);

            this.asyncLoop = this.asyncLoopFactory.Run($"Consensus Loop", async (token) =>
            {
                await this.PullerLoopAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.RunOnce);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public void Stop()
        {
            this.Puller.Dispose();
            this.asyncLoop?.Dispose();
            this.consensusLock.Dispose();
        }

        /// <summary>
        /// A puller method that will continuously loop and ask for the next block  in the chain from peers.
        /// The block will then be passed to the consensus validation.
        /// </summary>
        /// <remarks>
        /// If the <see cref="Block"/> returned from the puller is <c>null</c> that means the puller is signaling a reorg was detected.
        /// In this case a rewind of the <see cref="CoinView"/> db will be triggered to roll back consensus until a block is found that is in the best chain.
        /// </remarks>
        private async Task PullerLoopAsync()
        {
            this.logger.LogTrace("()");

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var validationContext = new ValidationContext();

                using (new StopwatchDisposable(o => this.ConsensusRules.PerformanceCounter.AddBlockFetchingTime(o)))
                {
                    // Save the current consensus tip to later check if it changed.
                    ChainedHeader consensusTip = this.Tip;

                    this.logger.LogTrace("Asking block puller to deliver next block.");

                    // This method will block until the next block is downloaded.
                    LookaheadResult lookaheadResult = this.Puller.NextBlock(this.nodeLifetime.ApplicationStopping);

                    if (lookaheadResult.Block == null)
                    {
                        using (await this.consensusLock.LockAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false))
                        {
                            this.logger.LogTrace("No block received from puller due to reorganization.");

                            if (!consensusTip.Equals(this.Tip))
                            {
                                this.logger.LogTrace("Consensus tip changed from '{0}' to '{1}', no rewinding.", consensusTip, this.Tip);
                                continue;
                            }

                            this.logger.LogTrace("Rewinding.");
                            await this.RewindCoinViewLockedAsync().ConfigureAwait(false);

                            continue;
                        }
                    }

                    validationContext.Block = lookaheadResult.Block;
                    validationContext.Peer = lookaheadResult.Peer;
                }

                this.logger.LogTrace("Block received from puller.");
                await this.AcceptBlockAsync(validationContext).ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Rewinds coinview to a block that exists on the current best chain.
        /// Also resets consensus tip, puller's tip and chain state's consensus tip to that block.
        /// </summary>
        /// <remarks>The caller of this method is responsible for holding <see cref="consensusLock"/>.</remarks>
        private async Task RewindCoinViewLockedAsync()
        {
            this.logger.LogTrace("()");

            ChainedHeader lastTip = this.Tip;
            CancellationToken token = this.nodeLifetime.ApplicationStopping;

            ChainedHeader rewinded = null;
            while (rewinded == null)
            {
                token.ThrowIfCancellationRequested();

                uint256 hash = await this.UTXOSet.Rewind().ConfigureAwait(false);
                rewinded = this.Chain.GetBlock(hash);
                if (rewinded == null)
                {
                    this.logger.LogTrace("Rewound to '{0}', which is still not a part of the current best chain, rewinding further.", hash);
                }
            }

            this.Tip = rewinded;
            this.Puller.SetLocation(rewinded);
            this.chainState.ConsensusTip = this.Tip;
            this.logger.LogInformation("Reorg detected, rewinding from '{0}' to '{1}'.", lastTip, this.Tip);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public async Task AcceptBlockAsync(ValidationContext validationContext)
        {
            this.logger.LogTrace("()");

            using (await this.consensusLock.LockAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false))
            {
                validationContext.RuleContext = this.ConsensusRules.CreateRuleContext(validationContext, this.Tip);

                // TODO: Once all code is migrated to rules this can be uncommented and the logic in this method moved to the IConsensusRules.AcceptBlockAsync()
                // await this.consensusRules.AcceptBlockAsync(blockValidationContext);

                try
                {
                    await this.ValidateAndExecuteBlockAsync(validationContext.RuleContext).ConfigureAwait(false);
                }
                catch (ConsensusErrorException ex)
                {
                    validationContext.Error = ex.ConsensusError;
                }

                if (validationContext.Error != null)
                {
                    uint256 rejectedBlockHash = validationContext.Block.GetHash();
                    this.logger.LogError("Block '{0}' rejected: {1}", rejectedBlockHash, validationContext.Error.Message);

                    // Check if the error is a consensus failure.
                    if (validationContext.Error == ConsensusErrors.InvalidPrevTip)
                    {
                        if (!this.Chain.Contains(this.Tip.HashBlock))
                        {
                            // Our consensus tip is not on the best chain, which means that the current block
                            // we are processing might be rejected only because of that. The consensus is on wrong chain
                            // and need to be reset.
                            await this.RewindCoinViewLockedAsync().ConfigureAwait(false);
                        }

                        this.logger.LogTrace("(-)[INVALID_PREV_TIP]");
                        return;
                    }

                    // Pull again.
                    this.Puller.SetLocation(this.Tip);

                    if (validationContext.Error == ConsensusErrors.BadWitnessNonceSize)
                    {
                        this.logger.LogInformation("You probably need witness information, activating witness requirement for peers.");
                        this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);
                        this.Puller.RequestOptions(TransactionOptions.Witness);

                        this.logger.LogTrace("(-)[BAD_WITNESS_NONCE_SIZE]");
                        return;
                    }

                    // Set the chain back to ConsensusLoop.Tip.
                    this.Chain.SetTip(this.Tip);
                    this.logger.LogTrace("Chain reverted back to block '{0}'.", this.Tip);

                    bool peerShouldGetBanned = validationContext.BanDurationSeconds != ValidationContext.BanDurationNoBan;
                    if ((validationContext.Peer != null) && peerShouldGetBanned)
                    {
                        int banDuration = validationContext.BanDurationSeconds == ValidationContext.BanDurationDefaultBan ? this.connectionManager.ConnectionSettings.BanTimeSeconds : validationContext.BanDurationSeconds;
                        this.peerBanning.BanAndDisconnectPeer(validationContext.Peer, banDuration, $"Invalid block received: {validationContext.Error.Message}");
                    }

                    if (validationContext.Error != ConsensusErrors.BadTransactionDuplicate)
                    {
                        // Since ChainHeadersBehavior check PoW, MarkBlockInvalid can't be spammed.
                        this.logger.LogError("Marking block '{0}' as invalid{1}.", rejectedBlockHash, validationContext.RejectUntil != null ? string.Format(" until {0:yyyy-MM-dd HH:mm:ss}", validationContext.RejectUntil.Value) : "");
                        this.chainState.MarkBlockInvalid(rejectedBlockHash, validationContext.RejectUntil);
                    }
                }
                else
                {
                    this.logger.LogTrace("Block '{0}' accepted.", this.Tip);

                    this.chainState.ConsensusTip = this.Tip;

                    bool forceFlush = this.FlushRequired();
                    await this.FlushAsync(forceFlush).ConfigureAwait(false);

                    if (this.Tip.ChainWork > this.Chain.Tip.ChainWork)
                    {
                        // This is a newly mined block.
                        this.Chain.SetTip(this.Tip);
                        this.Puller.SetLocation(this.Tip);

                        this.logger.LogDebug("Block extends best chain tip to '{0}'.", this.Tip);
                    }

                    this.signals.SignalBlockConnected(validationContext.Block);
                }
            }

            this.logger.LogTrace("(-):*.{0}='{1}',*.{2}='{3}'", nameof(validationContext.ChainedHeader), validationContext.ChainedHeader, nameof(validationContext.Error), validationContext.Error?.Message);
        }

        /// <summary>
        /// Calculates if coinview flush is required.
        /// </summary>
        /// <remarks>
        /// For blockchains with max reorg property flush is required when consensus tip is less than max reorg blocks behind the chain tip.
        /// If there is no max reorg property - flush is required when consensus tip timestamp is less than <see cref="FlushRequiredThresholdSeconds"/> behind the adjusted time.
        /// </remarks>
        private bool FlushRequired()
        {
            if (this.chainState.MaxReorgLength != 0)
                return this.Chain.Height - this.Tip.Height < this.chainState.MaxReorgLength;

            return this.Tip.Header.Time > this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp() - FlushRequiredThresholdSeconds;
        }

        /// <inheritdoc/>
        public void ValidateBlock(RuleContext context)
        {
            this.logger.LogTrace("()");

            this.ConsensusRules.ValidateAsync(context).GetAwaiter().GetResult();

            this.logger.LogTrace("(-)[OK]");
        }

        /// <summary>
        /// Validates a block using the consensus rules and executes it (processes it and adds it as a tip to consensus).
        /// </summary>
        /// <param name="context">A context that contains all information required to validate the block.</param>
        internal async Task ValidateAndExecuteBlockAsync(RuleContext context)
        {
            this.logger.LogTrace("()");

            await this.ConsensusRules.ValidateAndExecuteAsync(context);

            // Set the new tip.
            this.Tip = context.ValidationContext.ChainedHeader;
            this.logger.LogTrace("(-)[OK]");
        }

        /// <inheritdoc/>
        public async Task FlushAsync(bool force)
        {
            this.logger.LogTrace("({0}:{1})", nameof(force), force);

            if (this.UTXOSet is CachedCoinView cachedCoinView)
                await cachedCoinView.FlushAsync(force).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public Target GetNetworkDifficulty()
        {
            return this.Tip?.GetWorkRequired(this.nodeSettings.Network.Consensus);
        }

        /// <inheritdoc />
        public async Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            CoinViews.FetchCoinsResponse response = null;
            if (this.UTXOSet != null)
                response = await this.UTXOSet.FetchCoinsAsync(new[] { trxid }).ConfigureAwait(false);
            return response?.UnspentOutputs?.SingleOrDefault();
        }
    }
}
