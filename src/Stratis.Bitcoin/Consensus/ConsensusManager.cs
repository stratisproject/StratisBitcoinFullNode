using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus.PerformanceCounters.ConsensusManager;
using Stratis.Bitcoin.Consensus.ValidationResults;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Consensus
{
    /// <inheritdoc cref="IConsensusManager"/>
    public class ConsensusManager : IConsensusManager
    {
        /// <summary>
        /// Maximum memory in bytes that can be taken by the blocks that were downloaded but
        /// not yet validated or included to the consensus chain.
        /// </summary>
        private long maxUnconsumedBlocksDataBytes { get; set; }

        /// <summary>Queue consumption threshold in bytes.</summary>
        /// <remarks><see cref="toDownloadQueue"/> consumption will start if only we have more than this value of free memory.</remarks>
        private long ConsumptionThresholdBytes => this.maxUnconsumedBlocksDataBytes / 10;

        /// <summary>The maximum amount of blocks that can be assigned to <see cref="IBlockPuller"/> at the same time.</summary>
        private const int MaxBlocksToAskFromPuller = 10000;

        /// <summary>The minimum amount of slots that should be available to trigger asking block puller for blocks.</summary>
        private const int ConsumptionThresholdSlots = MaxBlocksToAskFromPuller / 10;

        /// <summary>The amount of blocks from consensus the node is considered to be synced.</summary>
        private const int ConsensusIsConsideredToBeSyncedMargin = 5;

        private readonly Network network;
        private readonly ILogger logger;
        private readonly IChainedHeaderTree chainedHeaderTree;
        private readonly IChainState chainState;
        private readonly IPartialValidator partialValidator;
        private readonly IFullValidator fullValidator;
        private readonly ISignals signals;
        private readonly IPeerBanning peerBanning;
        private readonly IBlockStore blockStore;
        private readonly IFinalizedBlockInfoRepository finalizedBlockInfo;
        private readonly IBlockPuller blockPuller;
        private readonly IIntegrityValidator integrityValidator;
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Connection manager of all the currently connected peers.</summary>
        private readonly IConnectionManager connectionManager;

        /// <inheritdoc />
        public ChainedHeader Tip { get; private set; }

        /// <inheritdoc />
        public IConsensusRuleEngine ConsensusRules { get; private set; }

        /// <summary>
        /// A container of call backs used by the download processes.
        /// </summary>
        internal class DownloadedCallbacks
        {
            /// <summary>The consensus code has requested this block, invoke the method <see cref="ProcessDownloadedBlock"/> when block is delivered.</summary>
            public bool ConsensusRequested { get; set; }

            /// <summary>List of delegates to call when block is delivered.</summary>
            public List<OnBlockDownloadedCallback> Callbacks { get; set; }
        }

        private readonly Dictionary<uint256, DownloadedCallbacks> callbacksByBlocksRequestedHash;

        /// <summary>Peers mapped by their ID.</summary>
        /// <remarks>This object has to be protected by <see cref="peerLock"/>.</remarks>
        private readonly Dictionary<int, INetworkPeer> peersByPeerId;

        private readonly Queue<BlockDownloadRequest> toDownloadQueue;

        /// <summary>Protects access to the <see cref="blockPuller"/>, <see cref="chainedHeaderTree"/>, <see cref="expectedBlockSizes"/> and <see cref="expectedBlockDataBytes"/>.</summary>
        private readonly object peerLock;

        private readonly IInitialBlockDownloadState ibdState;

        private readonly object blockRequestedLock;

        private readonly AsyncLock reorgLock;

        private long expectedBlockDataBytes;

        private readonly Dictionary<uint256, long> expectedBlockSizes;

        private readonly ConcurrentChain chain;

        private readonly ConsensusManagerPerformanceCounter performanceCounter;

        private bool isIbd;

        internal ConsensusManager(
            IChainedHeaderTree chainedHeaderTree,
            Network network,
            ILoggerFactory loggerFactory,
            IChainState chainState,
            IIntegrityValidator integrityValidator,
            IPartialValidator partialValidator,
            IFullValidator fullValidator,
            IConsensusRuleEngine consensusRules,
            IFinalizedBlockInfoRepository finalizedBlockInfo,
            ISignals signals,
            IPeerBanning peerBanning,
            IInitialBlockDownloadState ibdState,
            ConcurrentChain chain,
            IBlockPuller blockPuller,
            IBlockStore blockStore,
            IConnectionManager connectionManager,
            INodeStats nodeStats,
            INodeLifetime nodeLifetime,
            ConsensusSettings consensusSettings)
        {
            Guard.NotNull(chainedHeaderTree, nameof(chainedHeaderTree));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(integrityValidator, nameof(integrityValidator));
            Guard.NotNull(partialValidator, nameof(partialValidator));
            Guard.NotNull(fullValidator, nameof(fullValidator));
            Guard.NotNull(consensusRules, nameof(consensusRules));
            Guard.NotNull(finalizedBlockInfo, nameof(finalizedBlockInfo));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(peerBanning, nameof(peerBanning));
            Guard.NotNull(ibdState, nameof(ibdState));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(blockPuller, nameof(blockPuller));
            Guard.NotNull(blockStore, nameof(blockStore));
            Guard.NotNull(connectionManager, nameof(connectionManager));
            Guard.NotNull(nodeStats, nameof(nodeStats));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.network = network;
            this.chainState = chainState;
            this.integrityValidator = integrityValidator;
            this.partialValidator = partialValidator;
            this.fullValidator = fullValidator;
            this.ConsensusRules = consensusRules;
            this.signals = signals;
            this.peerBanning = peerBanning;
            this.blockStore = blockStore;
            this.finalizedBlockInfo = finalizedBlockInfo;
            this.chain = chain;
            this.connectionManager = connectionManager;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.chainedHeaderTree = chainedHeaderTree;

            this.peerLock = new object();
            this.reorgLock = new AsyncLock();
            this.blockRequestedLock = new object();
            this.expectedBlockDataBytes = 0;
            this.expectedBlockSizes = new Dictionary<uint256, long>();

            this.callbacksByBlocksRequestedHash = new Dictionary<uint256, DownloadedCallbacks>();
            this.peersByPeerId = new Dictionary<int, INetworkPeer>();
            this.toDownloadQueue = new Queue<BlockDownloadRequest>();
            this.performanceCounter = new ConsensusManagerPerformanceCounter();
            this.ibdState = ibdState;

            this.blockPuller = blockPuller;

            this.maxUnconsumedBlocksDataBytes = consensusSettings.MaxBlockMemoryInMB * 1024 * 1024;

            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 1000);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, 1000);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 1000);
        }

        /// <inheritdoc />
        /// <remarks>
        /// If <see cref="blockStore"/> is not <c>null</c> (block store is available) then all block headers in
        /// <see cref="chainedHeaderTree"/> will be marked as their block data is available.
        /// If store is not available the <see cref="ConsensusManager"/> won't be able to serve blocks from disk,
        /// instead all block requests that are not in memory will be sent to the <see cref="blockPuller"/>.
        /// </remarks>
        public async Task InitializeAsync(ChainedHeader chainTip)
        {
            Guard.NotNull(chainTip, nameof(chainTip));

            // TODO: consensus store
            // We should consider creating a consensus store class that will internally contain
            // coinview and it will abstract the methods `RewindAsync()` `GetBlockHashAsync()`

            uint256 consensusTipHash = await this.ConsensusRules.GetBlockHashAsync().ConfigureAwait(false);

            ChainedHeader pendingTip;

            while (true)
            {
                pendingTip = chainTip.FindAncestorOrSelf(consensusTipHash);

                if ((pendingTip != null) && (this.chainState.BlockStoreTip.Height >= pendingTip.Height))
                    break;

                this.logger.LogInformation("Consensus at height {0} is ahead of the block store at height {1}, rewinding consensus.", pendingTip, this.chainState.BlockStoreTip);

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                RewindState transitionState = await this.ConsensusRules.RewindAsync().ConfigureAwait(false);
                consensusTipHash = transitionState.BlockHash;
            }

            this.chainedHeaderTree.Initialize(pendingTip);

            this.SetConsensusTip(pendingTip);

            this.blockPuller.Initialize(this.BlockDownloaded);

            this.isIbd = this.ibdState.IsInitialBlockDownload();
            this.blockPuller.OnIbdStateChanged(this.isIbd);
        }

        /// <inheritdoc />
        public ConnectNewHeadersResult HeadersPresented(INetworkPeer peer, List<BlockHeader> headers, bool triggerDownload = true)
        {
            Guard.NotNull(peer, nameof(peer));
            Guard.NotNull(headers, nameof(headers));

            ConnectNewHeadersResult connectNewHeadersResult;

            lock (this.peerLock)
            {
                int peerId = peer.Connection.Id;

                connectNewHeadersResult = this.chainedHeaderTree.ConnectNewHeaders(peerId, headers);

                if (connectNewHeadersResult == null)
                {
                    this.logger.LogTrace("(-)[NO_HEADERS_CONNECTED]:null");
                    return null;
                }

                if (connectNewHeadersResult.Consumed == null)
                {
                    this.logger.LogTrace("(-)[NOTHING_CONSUMED]");
                    return connectNewHeadersResult;
                }

                this.chainState.IsAtBestChainTip = this.IsConsensusConsideredToBeSyncedLocked();

                this.blockPuller.NewPeerTipClaimed(peer, connectNewHeadersResult.Consumed);

                if (!this.peersByPeerId.ContainsKey(peerId))
                {
                    this.peersByPeerId.Add(peerId, peer);
                    this.logger.LogTrace("New peer with ID {0} was added.", peerId);
                }
            }

            if (triggerDownload && (connectNewHeadersResult.DownloadTo != null))
                this.DownloadBlocks(connectNewHeadersResult.ToArray());

            return connectNewHeadersResult;
        }

        /// <inheritdoc />
        public void PeerDisconnected(int peerId)
        {
            lock (this.peerLock)
            {
                this.PeerDisconnectedLocked(peerId);
            }
        }

        /// <inheritdoc />
        public async Task<ChainedHeader> BlockMinedAsync(Block block)
        {
            Guard.NotNull(block, nameof(block));

            ValidationContext validationContext;

            using (await this.reorgLock.LockAsync().ConfigureAwait(false))
            {
                ChainedHeader chainedHeader;

                lock (this.peerLock)
                {
                    if (block.Header.HashPrevBlock != this.Tip.HashBlock)
                    {
                        this.logger.LogTrace("(-)[BLOCKMINED_INVALID_PREVIOUS_TIP]:null");
                        return null;
                    }

                    // This might throw ConsensusErrorException but we don't wanna catch it because miner will catch it.
                    chainedHeader = this.chainedHeaderTree.CreateChainedHeaderOfMinedBlock(block);
                }

                validationContext = await this.partialValidator.ValidateAsync(chainedHeader, block).ConfigureAwait(false);

                if (validationContext.Error == null)
                {
                    bool fullValidationRequired;

                    lock (this.peerLock)
                    {
                        this.chainedHeaderTree.PartialValidationSucceeded(chainedHeader, out fullValidationRequired);
                    }

                    if (fullValidationRequired)
                    {
                        ConnectBlocksResult fullValidationResult = await this.FullyValidateLockedAsync(validationContext.ChainedHeaderToValidate, true).ConfigureAwait(false);
                        if (!fullValidationResult.Succeeded)
                        {
                            this.logger.LogTrace("Miner produced an invalid block, full validation failed: {0}", fullValidationResult.Error.Message);
                            this.logger.LogTrace("(-)[FULL_VALIDATION_FAILED]");
                            throw new ConsensusException(fullValidationResult.Error.Message);
                        }
                    }
                    else
                    {
                        this.logger.LogTrace("(-)[FULL_VALIDATION_WAS_NOT_REQUIRED]");
                        throw new ConsensusException("Full validation was not required.");
                    }
                }
                else
                {
                    lock (this.peerLock)
                    {
                        this.chainedHeaderTree.PartialOrFullValidationFailed(chainedHeader);
                    }

                    this.logger.LogError("Miner produced an invalid block, partial validation failed: {0}", validationContext.Error.Message);
                    this.logger.LogTrace("(-)[PARTIAL_VALIDATION_FAILED]");
                    throw new ConsensusException(validationContext.Error.ToString());
                }
            }

            return validationContext.ChainedHeaderToValidate;
        }

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the event but only if the node is not being shut down at the moment.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="peerLock"/>.</remarks>
        /// <param name="peerId">The peer that was disconnected.</param>
        private void PeerDisconnectedLocked(int peerId)
        {
            bool removed = this.peersByPeerId.Remove(peerId);

            if (removed)
            {
                bool shuttingDown = this.nodeLifetime.ApplicationStopping.IsCancellationRequested;

                // Update the components only in case we are not shutting down. In case we update CHT during
                // shutdown there will be a huge performance hit when we have a lot of headers in front of our
                // consensus and then disconnect last peer claiming such a chain. CHT will disconnect headers
                // one by one. This is not needed during the shutdown.
                if (!shuttingDown)
                {
                    this.chainedHeaderTree.PeerDisconnected(peerId);
                    this.blockPuller.PeerDisconnected(peerId);
                    this.ProcessDownloadQueueLocked();
                }
                else
                    this.logger.LogDebug("Node is shutting down therefore underlying components won't be updated.");
            }
            else
                this.logger.LogTrace("Peer {0} was already removed.", peerId);
        }

        /// <summary>
        /// A callback that is triggered when a block that <see cref="ConsensusManager"/> requested was downloaded.
        /// </summary>
        private void ProcessDownloadedBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            if (chainedHeaderBlock.Block == null)
            {
                this.logger.LogTrace("(-)[DOWNLOAD_FAILED_NO_PEERS_CLAIMED_BLOCK]:'{0}'", chainedHeaderBlock.ChainedHeader);
                return;
            }

            bool partialValidationRequired = false;

            lock (this.peerLock)
            {
                partialValidationRequired = this.chainedHeaderTree.BlockDataDownloaded(chainedHeaderBlock.ChainedHeader, chainedHeaderBlock.Block);
            }

            this.logger.LogTrace("Partial validation is{0} required.", partialValidationRequired ? string.Empty : " NOT");

            if (partialValidationRequired)
                this.partialValidator.StartPartialValidation(chainedHeaderBlock.ChainedHeader, chainedHeaderBlock.Block, this.OnPartialValidationCompletedCallbackAsync);
        }

        private async Task OnPartialValidationCompletedCallbackAsync(ValidationContext validationContext)
        {
            if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                this.logger.LogTrace("(-)[NODE_DISPOSED]");
                return;
            }

            if (validationContext.Error == null)
            {
                await this.OnPartialValidationSucceededAsync(validationContext.ChainedHeaderToValidate).ConfigureAwait(false);
            }
            else
            {
                var peersToBan = new List<INetworkPeer>();

                if (validationContext.MissingServices != null)
                {
                    this.connectionManager.AddDiscoveredNodesRequirement(validationContext.MissingServices.Value);
                    this.blockPuller.RequestPeerServices(validationContext.MissingServices.Value);

                    this.logger.LogTrace("(-)[MISSING_SERVICES]");
                    return;
                }

                lock (this.peerLock)
                {
                    List<int> peerIdsToBan = this.chainedHeaderTree.PartialOrFullValidationFailed(validationContext.ChainedHeaderToValidate);

                    this.logger.LogDebug("Validation of block '{0}' failed, banning and disconnecting {1} peers.", validationContext.ChainedHeaderToValidate, peerIdsToBan.Count);

                    foreach (int peerId in peerIdsToBan)
                    {
                        if (this.peersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                            peersToBan.Add(peer);
                    }
                }

                foreach (INetworkPeer peer in peersToBan)
                    this.peerBanning.BanAndDisconnectPeer(peer.RemoteSocketEndpoint, validationContext.BanDurationSeconds, $"Invalid block received: {validationContext.Error.Message}");
            }
        }

        /// <summary>
        /// Handles a situation when partial validation of a block was successful. Informs CHT about
        /// finishing partial validation process and starting a new partial validation or full validation.
        /// </summary>
        /// <param name="chainedHeader">Header of a block which validation was successful.</param>
        private async Task OnPartialValidationSucceededAsync(ChainedHeader chainedHeader)
        {
            using (this.performanceCounter.MeasureTotalConnectionTime())
            {
                List<ChainedHeaderBlock> chainedHeaderBlocksToValidate;
                ConnectBlocksResult connectBlocksResult = null;

                using (await this.reorgLock.LockAsync().ConfigureAwait(false))
                {
                    bool fullValidationRequired;

                    lock (this.peerLock)
                    {
                        chainedHeaderBlocksToValidate = this.chainedHeaderTree.PartialValidationSucceeded(chainedHeader, out fullValidationRequired);
                    }

                    this.logger.LogTrace("Full validation is{0} required.", fullValidationRequired ? string.Empty : " NOT");

                    if (fullValidationRequired)
                    {
                        connectBlocksResult = await this.FullyValidateLockedAsync(chainedHeader).ConfigureAwait(false);
                    }
                }

                if (connectBlocksResult != null)
                {
                    if (connectBlocksResult.PeersToBan != null)
                    {
                        var peersToBan = new List<INetworkPeer>();

                        lock (this.peerLock)
                        {
                            foreach (int peerId in connectBlocksResult.PeersToBan)
                            {
                                if (this.peersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                                    peersToBan.Add(peer);
                            }
                        }

                        this.logger.LogTrace("{0} peers will be banned.", peersToBan.Count);

                        foreach (INetworkPeer peer in peersToBan)
                            this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, connectBlocksResult.BanDurationSeconds, connectBlocksResult.BanReason);
                    }

                    if (connectBlocksResult.ConsensusTipChanged)
                        await this.NotifyBehaviorsOnConsensusTipChangedAsync().ConfigureAwait(false);

                    lock (this.peerLock)
                    {
                        this.ProcessDownloadQueueLocked();
                    }
                }

                // If more blocks are available continue validation.
                if (chainedHeaderBlocksToValidate != null)
                {
                    // Validate the next blocks if validation was not needed, or if needed then it succeeded.
                    if ((connectBlocksResult == null) || connectBlocksResult.Succeeded)
                    {
                        this.logger.LogTrace("Partial validation of {0} block will be started.", chainedHeaderBlocksToValidate.Count);

                        // Start validating all next blocks that come after the current block,
                        // all headers in this list have the blocks present in the header.
                        foreach (ChainedHeaderBlock toValidate in chainedHeaderBlocksToValidate)
                            this.partialValidator.StartPartialValidation(toValidate.ChainedHeader, toValidate.Block, this.OnPartialValidationCompletedCallbackAsync);
                    }
                }
            }
        }

        /// <summary>
        /// Notifies the chained header behaviors of all connected peers when a consensus tip is changed.
        /// Consumes headers from their caches if there are any.
        /// </summary>
        private async Task NotifyBehaviorsOnConsensusTipChangedAsync()
        {
            var behaviors = new List<ConsensusManagerBehavior>();

            lock (this.peerLock)
            {
                foreach (INetworkPeer peer in this.peersByPeerId.Values)
                    behaviors.Add(peer.Behavior<ConsensusManagerBehavior>());
            }

            var blocksToDownload = new List<ConnectNewHeadersResult>();

            foreach (ConsensusManagerBehavior consensusManagerBehavior in behaviors)
            {
                ConnectNewHeadersResult connectNewHeadersResult = await consensusManagerBehavior.ConsensusTipChangedAsync().ConfigureAwait(false);

                int? peerId = consensusManagerBehavior.AttachedPeer?.Connection?.Id;

                if (peerId == null)
                    continue;

                if (connectNewHeadersResult?.DownloadTo == null)
                {
                    this.logger.LogTrace("No new blocks to download were presented by peer ID {0}.", peerId);
                    continue;
                }

                blocksToDownload.Add(connectNewHeadersResult);
                this.logger.LogTrace("{0} headers were added to download by peer ID {1}.", connectNewHeadersResult.DownloadTo.Height - connectNewHeadersResult.DownloadFrom.Height + 1, peerId);
            }

            foreach (ConnectNewHeadersResult newHeaders in blocksToDownload)
                this.DownloadBlocks(newHeaders.ToArray());
        }

        /// <summary>Attempt to switch to new chain, which may require rewinding blocks from the current chain.</summary>
        /// <remarks>
        /// It is possible that during connection we find out that blocks that we tried to connect are invalid and we switch back to original chain.
        /// Should be locked by <see cref="reorgLock"/>.
        /// </remarks>
        /// <param name="newTip">Tip of the chain that will become the tip of our consensus chain if full validation will succeed.</param>
        /// <param name="blockMined">Was the block mined or received from the network.</param>
        /// <returns>Validation related information.</returns>
        private async Task<ConnectBlocksResult> FullyValidateLockedAsync(ChainedHeader newTip, bool blockMined = false)
        {
            ChainedHeader oldTip = this.Tip;

            ChainedHeader fork = oldTip.FindFork(newTip);

            if (fork == newTip)
            {
                // The new header is behind the current tip this is a bug.
                this.logger.LogCritical("New header '{0}' is behind the current tip '{1}'.", newTip, oldTip);
                this.logger.LogTrace("(-)[INVALID_NEW_TIP]");
                throw new ConsensusException("New tip must be ahead of old tip.");
            }

            // If the new block is not on the current chain as our current consensus tip then rewind consensus tip to the common fork.
            bool isExtension = fork == oldTip;

            List<ChainedHeaderBlock> disconnectedBlocks = null;

            if (!isExtension)
                disconnectedBlocks = await this.RewindToForkPointAsync(fork, oldTip).ConfigureAwait(false);

            List<ChainedHeaderBlock> blocksToConnect = await this.TryGetBlocksToConnectAsync(newTip, fork.Height + 1).ConfigureAwait(false);

            // Sanity check. This should never happen.
            if (blocksToConnect == null)
            {
                this.logger.LogCritical("Blocks to connect are missing!");
                this.logger.LogTrace("(-)[NO_BLOCK_TO_CONNECT]");
                throw new ConsensusException("Blocks to connect are missing!");
            }

            ConnectBlocksResult connectBlockResult = await this.ConnectChainAsync(blocksToConnect, blockMined).ConfigureAwait(false);

            if (connectBlockResult.Succeeded)
            {
                if (!isExtension)
                {
                    // A block might have been set to null for blocks more then 100 block behind the tip.
                    // As this chain is not the longest chain anymore we need to put the blocks back to the header (they will not be available in store),
                    // this is in case a reorg longer then 100 may happen later and we will need the blocks to connect on top of CT.
                    // This might cause uncontrolled memory changes but big reorgs are not common and a chain will anyway get disconnected when the fork is more then 500 blocks.
                    foreach (ChainedHeaderBlock disconnectedBlock in disconnectedBlocks)
                    {
                        this.logger.LogTrace("[DISCONNECTED_BLOCK_STATE]{0}", disconnectedBlock.ChainedHeader);
                        this.logger.LogTrace("[DISCONNECTED_BLOCK_STATE]{0}", disconnectedBlock.ChainedHeader.Previous);

                        this.chainedHeaderTree.BlockRewinded(disconnectedBlock);
                    }
                }

                this.logger.LogTrace("(-)[SUCCEEDED]:'{0}'", connectBlockResult);
                return connectBlockResult;
            }

            if (connectBlockResult.LastValidatedBlockHeader != null)
            {
                // Block validation failed we need to rewind any blocks that were added to the chain.
                await this.RewindToForkPointAsync(fork, connectBlockResult.LastValidatedBlockHeader).ConfigureAwait(false);
            }

            if (isExtension)
            {
                this.logger.LogTrace("(-)[DIDNT_REWIND]:'{0}'", connectBlockResult);
                return connectBlockResult;
            }

            // Reconnect disconnected blocks.
            ConnectBlocksResult reconnectionResult = await this.ReconnectOldChainAsync(disconnectedBlocks).ConfigureAwait(false);

            // Add peers that needed to be banned as a result of a failure to connect blocks.
            // Otherwise they get lost as we are returning a different ConnnectBlocksResult.
            reconnectionResult.PeersToBan = connectBlockResult.PeersToBan;

            return reconnectionResult;
        }

        /// <summary>Rewinds to fork point.</summary>
        /// <param name="fork">The fork point. It can't be ahead of <paramref name="oldTip"/>.</param>
        /// <param name="oldTip">The old tip.</param>
        /// <exception cref="ConsensusException">Thrown in case <paramref name="fork"/> is ahead of the <paramref name="oldTip"/>.</exception>
        /// <returns>List of blocks that were disconnected.</returns>
        private async Task<List<ChainedHeaderBlock>> RewindToForkPointAsync(ChainedHeader fork, ChainedHeader oldTip)
        {
            // This is sanity check and should never happen.
            if (fork.Height > oldTip.Height)
            {
                this.logger.LogTrace("(-)[INVALID_FORK_POINT]");
                throw new ConsensusException("Fork can't be ahead of tip!");
            }

            // Save blocks that will be disconnected in case we will need to
            // reconnect them. This might happen if connection of a new chain fails.
            var disconnectedBlocks = new List<ChainedHeaderBlock>(oldTip.Height - fork.Height);

            ChainedHeader current = oldTip;

            while (current != fork)
            {
                await this.ConsensusRules.RewindAsync().ConfigureAwait(false);

                lock (this.peerLock)
                {
                    this.SetConsensusTipInternalLocked(current.Previous);
                }

                Block block = current.Block;

                if (block == null)
                {
                    this.logger.LogTrace("Block '{0}' wasn't cached. Loading it from the database.", current.HashBlock);
                    block = await this.blockStore.GetBlockAsync(current.HashBlock).ConfigureAwait(false);

                    if (block == null)
                    {
                        // Sanity check. Block being disconnected should always be in the block store before we rewind.
                        this.logger.LogTrace("(-)[BLOCK_NOT_FOUND]");
                        throw new Exception("Block that is about to be rewinded wasn't found in cache or database!");
                    }
                }

                var disconnectedBlock = new ChainedHeaderBlock(block, current);

                disconnectedBlocks.Add(disconnectedBlock);

                using (this.performanceCounter.MeasureBlockDisconnectedSignal())
                {
                    this.signals.SignalBlockDisconnected(disconnectedBlock);
                }

                current = current.Previous;
            }

            disconnectedBlocks.Reverse();

            this.logger.LogInformation("Reorg from block '{0}' to '{1}'", oldTip, fork);

            return disconnectedBlocks;
        }

        /// <summary>Connects new chain.</summary>
        /// <param name="blocksToConnect">List of blocks to connect.</param>
        /// <param name="blockMined">Was the block mined or received from the network.</param>
        private async Task<ConnectBlocksResult> ConnectChainAsync(List<ChainedHeaderBlock> blocksToConnect, bool blockMined = false)
        {
            ChainedHeader lastValidatedBlockHeader = null;
            ConnectBlocksResult connectBlockResult = null;

            foreach (ChainedHeaderBlock blockToConnect in blocksToConnect)
            {
                using (this.performanceCounter.MeasureBlockConnectionFV())
                {
                    connectBlockResult = await this.ConnectBlockAsync(blockToConnect).ConfigureAwait(false);

                    if (!connectBlockResult.Succeeded)
                    {
                        connectBlockResult.LastValidatedBlockHeader = lastValidatedBlockHeader;

                        this.logger.LogTrace("(-)[FAILED_TO_CONNECT]:'{0}'", connectBlockResult);
                        return connectBlockResult;
                    }

                    lastValidatedBlockHeader = blockToConnect.ChainedHeader;

                    // Block connected successfully.
                    lock (this.peerLock)
                    {
                        this.SetConsensusTipInternalLocked(lastValidatedBlockHeader);
                    }

                    if (this.network.Consensus.MaxReorgLength != 0)
                    {
                        int newFinalizedHeight = blockToConnect.ChainedHeader.Height - (int)this.network.Consensus.MaxReorgLength;

                        if (newFinalizedHeight > 0)
                        {
                            uint256 newFinalizedHash = blockToConnect.ChainedHeader.GetAncestor(newFinalizedHeight).HashBlock;

                            this.finalizedBlockInfo.SaveFinalizedBlockHashAndHeight(newFinalizedHash, newFinalizedHeight);
                        }
                    }
                }

                using (this.performanceCounter.MeasureBlockConnectedSignal())
                {
                    this.signals.SignalBlockConnected(blockToConnect);
                }
            }

            // After successfully connecting all blocks set the tree tip and claim the branch.
            List<int> peersToResync = this.SetConsensusTip(lastValidatedBlockHeader, blockMined);

            // Disconnect peers that are not relevant anymore.
            await this.ResyncPeersAsync(peersToResync).ConfigureAwait(false);

            return connectBlockResult;
        }

        /// <summary>Reconnects the old chain.</summary>
        /// <param name="blocksToReconnect">List of blocks to reconnect.</param>
        private async Task<ConnectBlocksResult> ReconnectOldChainAsync(List<ChainedHeaderBlock> blocksToReconnect)
        {
            // Connect back the old blocks.
            ConnectBlocksResult connectBlockResult = await this.ConnectChainAsync(blocksToReconnect).ConfigureAwait(false);
            if (connectBlockResult.Succeeded)
            {
                // Even though reconnection was successful we return result with success == false because
                // full validation of the chain we originally wanted to connect was failed.
                var result = new ConnectBlocksResult(false) { ConsensusTipChanged = false, PeersToBan = new List<int>() };
                return result;
            }

            // We failed to jump back on the previous chain after a failed reorg.
            // And we failed to reconnect the old chain, database might be corrupted.
            this.logger.LogError("A critical error has prevented reconnecting blocks, error = {0}", connectBlockResult.Error);
            this.logger.LogTrace("(-)[FAILED_TO_RECONNECT]");
            throw new ConsensusException("A critical error has prevented reconnecting blocks.");
        }

        /// <summary>
        /// Informs <see cref="ConsensusManagerBehavior"/> of each peer
        /// to be resynced and simulates disconnection of the peer.
        /// </summary>
        /// <param name="peerIds">List of peer IDs to resync.</param>
        private async Task ResyncPeersAsync(List<int> peerIds)
        {
            var resyncTasks = new List<Task>(peerIds.Count);

            lock (this.peerLock)
            {
                foreach (int peerId in peerIds)
                {
                    if (this.peersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                    {
                        this.logger.LogTrace("Resyncing peer ID {0}.", peerId);

                        Task task = peer.Behavior<ConsensusManagerBehavior>().ResetPeerTipInformationAndSyncAsync();
                        resyncTasks.Add(task);
                    }
                    else
                        this.logger.LogTrace("Peer ID {0} was removed already.", peerId);

                    // Simulate peer disconnection to remove their data from internal structures.
                    this.PeerDisconnectedLocked(peerId);
                }
            }

            await Task.WhenAll(resyncTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to connect a block to a chain with specified tip.
        /// </summary>
        /// <param name="blockToConnect">Block to connect.</param>
        /// <exception cref="ConsensusException">Thrown in case CHT is not in a consistent state.</exception>
        private async Task<ConnectBlocksResult> ConnectBlockAsync(ChainedHeaderBlock blockToConnect)
        {
            if ((blockToConnect.ChainedHeader.BlockValidationState != ValidationState.PartiallyValidated) &&
                (blockToConnect.ChainedHeader.BlockValidationState != ValidationState.FullyValidated))
            {
                this.logger.LogError("Block '{0}' must be partially or fully validated but it is {1}.", blockToConnect, blockToConnect.ChainedHeader.BlockValidationState);
                this.logger.LogTrace("(-)[BLOCK_INVALID_STATE]");
                throw new ConsensusException("Block must be partially or fully validated.");
            }

            // Call the validation engine.
            ValidationContext validationContext = await this.fullValidator.ValidateAsync(blockToConnect.ChainedHeader, blockToConnect.Block).ConfigureAwait(false);

            if (validationContext.Error != null)
            {
                List<int> badPeers;

                lock (this.peerLock)
                {
                    badPeers = this.chainedHeaderTree.PartialOrFullValidationFailed(blockToConnect.ChainedHeader);
                }

                var failureResult = new ConnectBlocksResult(false)
                {
                    BanDurationSeconds = validationContext.BanDurationSeconds,
                    BanReason = validationContext.Error.Message,
                    ConsensusTipChanged = false,
                    Error = validationContext.Error,
                    PeersToBan = badPeers
                };

                this.logger.LogTrace("(-)[FAILED]:'{0}'", failureResult);
                return failureResult;
            }

            lock (this.peerLock)
            {
                this.chainedHeaderTree.FullValidationSucceeded(blockToConnect.ChainedHeader);

                this.chainState.IsAtBestChainTip = this.IsConsensusConsideredToBeSyncedLocked();
            }

            var result = new ConnectBlocksResult(true) { ConsensusTipChanged = true };

            return result;
        }

        /// <summary>Try to find all blocks between two headers.</summary>
        /// <returns>Collection of blocks that were loaded. In case at least one block was not present <c>null</c> will be returned.</returns>
        private async Task<List<ChainedHeaderBlock>> TryGetBlocksToConnectAsync(ChainedHeader proposedNewTip, int heightOfFirstBlock)
        {
            ChainedHeader currentHeader = proposedNewTip;
            var chainedHeaderBlocks = new List<ChainedHeaderBlock>();

            while (currentHeader.Height >= heightOfFirstBlock)
            {
                ChainedHeaderBlock chainedHeaderBlock = await this.GetBlockDataAsync(currentHeader.HashBlock).ConfigureAwait(false);

                if (chainedHeaderBlock?.Block == null)
                {
                    this.logger.LogError("Block '{0}' wasn't loaded from store!", currentHeader);
                    this.logger.LogTrace("(-):null");
                    return null;
                }

                chainedHeaderBlocks.Add(chainedHeaderBlock);
                currentHeader = currentHeader.Previous;
            }

            chainedHeaderBlocks.Reverse();

            return chainedHeaderBlocks;
        }

        /// <summary>Sets the consensus tip.</summary>
        /// <param name="newTip">New consensus tip.</param>
        /// <param name="blockMined">Was the block mined or received from the network.</param>
        private List<int> SetConsensusTip(ChainedHeader newTip, bool blockMined = false)
        {
            lock (this.peerLock)
            {
                List<int> peerIdsToResync = this.chainedHeaderTree.ConsensusTipChanged(newTip, blockMined);

                this.SetConsensusTipInternalLocked(newTip);

                bool ibd = this.ibdState.IsInitialBlockDownload();

                if (ibd != this.isIbd)
                    this.blockPuller.OnIbdStateChanged(ibd);

                this.isIbd = ibd;

                return peerIdsToResync;
            }
        }

        /// <summary>Updates all internal values with the new tip.</summary>
        /// <remarks>Have to be locked by <see cref="peerLock"/>.</remarks>
        /// <param name="newTip">New consensus tip.</param>
        private void SetConsensusTipInternalLocked(ChainedHeader newTip)
        {
            this.Tip = newTip;

            this.chainState.ConsensusTip = this.Tip;
            this.chain.SetTip(this.Tip);
        }

        /// <summary>
        /// Request a list of block headers to download their respective blocks.
        /// If <paramref name="chainedHeaders"/> is not an array of consecutive headers it will be split to batches of consecutive header requests.
        /// Callbacks of all entries are added to <see cref="callbacksByBlocksRequestedHash"/>. If a block header was already requested
        /// to download and not delivered yet, it will not be requested again, instead just it's callback will be called when the block arrives.
        /// </summary>
        /// <param name="chainedHeaders">Array of chained headers to download.</param>
        /// <param name="onBlockDownloadedCallback">A callback to call when the block was downloaded.</param>
        private void DownloadBlocks(ChainedHeader[] chainedHeaders, OnBlockDownloadedCallback onBlockDownloadedCallback = null)
        {
            var downloadRequests = new List<BlockDownloadRequest>();

            BlockDownloadRequest request = null;
            ChainedHeader previousHeader = null;

            lock (this.blockRequestedLock)
            {
                foreach (ChainedHeader chainedHeader in chainedHeaders)
                {
                    bool blockAlreadyAsked = this.callbacksByBlocksRequestedHash.TryGetValue(chainedHeader.HashBlock, out DownloadedCallbacks downloadedCallbacks);

                    if (!blockAlreadyAsked)
                    {
                        downloadedCallbacks = new DownloadedCallbacks();
                        this.callbacksByBlocksRequestedHash.Add(chainedHeader.HashBlock, downloadedCallbacks);
                    }
                    else
                    {
                        this.logger.LogTrace("Registered additional callback for the block '{0}'.", chainedHeader);
                    }

                    if (onBlockDownloadedCallback == null)
                    {
                        downloadedCallbacks.ConsensusRequested = true;
                    }
                    else
                    {
                       if (downloadedCallbacks.Callbacks == null)
                           downloadedCallbacks.Callbacks = new List<OnBlockDownloadedCallback>();

                       downloadedCallbacks.Callbacks.Add(onBlockDownloadedCallback);
                    }

                    bool blockIsNotConsecutive = (previousHeader != null) && (chainedHeader.Previous.HashBlock != previousHeader.HashBlock);

                    if (blockIsNotConsecutive || blockAlreadyAsked)
                    {
                        if (request != null)
                        {
                            downloadRequests.Add(request);
                            request = null;
                        }

                        if (blockAlreadyAsked)
                        {
                            previousHeader = null;
                            continue;
                        }
                    }

                    if (request == null)
                        request = new BlockDownloadRequest { BlocksToDownload = new List<ChainedHeader>() };

                    request.BlocksToDownload.Add(chainedHeader);
                    previousHeader = chainedHeader;
                }

                if (request != null)
                    downloadRequests.Add(request);

                lock (this.peerLock)
                {
                    foreach (BlockDownloadRequest downloadRequest in downloadRequests)
                        this.toDownloadQueue.Enqueue(downloadRequest);

                    this.ProcessDownloadQueueLocked();
                }
            }
        }

        /// <summary>Method that is provided as a callback to <see cref="IBlockPuller"/>.</summary>
        private void BlockDownloaded(uint256 blockHash, Block block, int peerId)
        {
            if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                this.logger.LogTrace("(-)[NODE_DISPOSED]");
                return;
            }

            ChainedHeader chainedHeader = null;
            bool reassignDownload = false;

            lock (this.peerLock)
            {
                if (this.expectedBlockSizes.TryGetValue(blockHash, out long expectedSize))
                {
                    this.expectedBlockDataBytes -= expectedSize;
                    this.expectedBlockSizes.Remove(blockHash);
                    this.logger.LogTrace("Expected block data bytes was set to {0} and we are expecting {1} blocks to be delivered.", this.expectedBlockDataBytes, this.expectedBlockSizes.Count);
                }
                else
                {
                    // This means the puller has not filtered blocks correctly.
                    this.logger.LogError("Unsolicited block '{0}'.", blockHash);
                    this.logger.LogTrace("(-)[UNSOLICITED_BLOCK]");
                    throw new InvalidOperationException("Unsolicited block");
                }

                chainedHeader = this.chainedHeaderTree.GetChainedHeader(blockHash);
                if (chainedHeader == null)
                {
                    lock (this.blockRequestedLock)
                    {
                        this.callbacksByBlocksRequestedHash.Remove(blockHash);
                    }

                    this.logger.LogTrace("(-)[CHAINED_HEADER_NOT_FOUND]");
                    return;
                }

                if (block == null)
                {
                    // A race conditions exists where if we attempted a download of a block but all the peers disconnected and then a peer presented the header
                    // again, we dont re-download the block.
                    if (chainedHeader.BlockDataAvailability == BlockDataAvailabilityState.BlockRequired)
                    {
                        // We need to remove the current callback so that it can be re-assigned for download.
                        lock (this.blockRequestedLock)
                        {
                            this.callbacksByBlocksRequestedHash.Remove(blockHash);
                        }

                        reassignDownload = true;
                    }
                    else
                        this.logger.LogTrace("Block download failed but will not be reassigned as it's state is {0}", chainedHeader.BlockDataAvailability);
                }
            }

            if (reassignDownload)
            {
                this.DownloadBlocks(new[] { chainedHeader });
                this.logger.LogWarning("Downloading block for '{0}' failed, it will be enqueued again.", chainedHeader);
                this.logger.LogTrace("(-)[BLOCK_DOWNLOAD_FAILED_REASSIGNED]");
                return;
            }

            if (block != null)
            {
                ValidationContext result = this.integrityValidator.VerifyBlockIntegrity(chainedHeader, block);

                if (result.Error != null)
                {
                    // When integrity validation fails we want to ban only the particular peer that provided the invalid block.
                    // Integrity validation failing for this block doesn't automatically make other blocks with the same hash invalid,
                    // therefore banning other peers that claim to be on a chain that contains a block with the same hash is not required.
                    if (this.peersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                        this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, result.BanDurationSeconds, $"Integrity validation failed: {result.Error.Message}");

                    lock (this.peerLock)
                    {
                        // Ask block puller to deliver this block again. Do it with high priority and avoid normal queue.
                        this.blockPuller.RequestBlocksDownload(new List<ChainedHeader>() { chainedHeader }, true);
                    }

                    this.logger.LogTrace("(-)[INTEGRITY_VERIFICATION_FAILED]");
                    return;
                }
            }
            else
            {
                this.logger.LogDebug("Block '{0}' failed to be delivered.", blockHash);
            }

            DownloadedCallbacks downloadedCallbacks = null;

            lock (this.blockRequestedLock)
            {
                if (this.callbacksByBlocksRequestedHash.TryGetValue(blockHash, out downloadedCallbacks))
                    this.callbacksByBlocksRequestedHash.Remove(blockHash);
            }

            if (downloadedCallbacks != null)
            {
                var chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

                if (downloadedCallbacks.ConsensusRequested)
                {
                    this.ProcessDownloadedBlock(chainedHeaderBlock);
                }

                if (downloadedCallbacks.Callbacks != null)
                {
                    this.logger.LogTrace("Calling {0} callbacks for block '{1}'.", downloadedCallbacks.Callbacks.Count, chainedHeader);
                    foreach (OnBlockDownloadedCallback blockDownloadedCallback in downloadedCallbacks.Callbacks)
                        blockDownloadedCallback(chainedHeaderBlock);
                }
            }
        }

        /// <inheritdoc />
        public async Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            Guard.NotNull(blockHashes, nameof(blockHashes));
            Guard.NotNull(onBlockDownloadedCallback, nameof(onBlockDownloadedCallback));

            var blocksToDownload = new List<ChainedHeader>();

            foreach (uint256 blockHash in blockHashes)
            {
                ChainedHeaderBlock chainedHeaderBlock = await this.GetBlockDataAsync(blockHash).ConfigureAwait(false);

                if ((chainedHeaderBlock == null) || (chainedHeaderBlock.Block != null))
                {
                    if (chainedHeaderBlock != null)
                        this.logger.LogTrace("Block data loaded for hash '{0}', calling the callback.", blockHash);
                    else
                        this.logger.LogTrace("Chained header not found for hash '{0}'.", blockHash);

                    onBlockDownloadedCallback(chainedHeaderBlock);
                }
                else
                {
                    blocksToDownload.Add(chainedHeaderBlock.ChainedHeader);
                    this.logger.LogTrace("Block hash '{0}' is queued for download.", blockHash);
                }
            }

            if (blocksToDownload.Count != 0)
            {
                this.logger.LogTrace("Asking block puller for {0} blocks.", blocksToDownload.Count);
                this.DownloadBlocks(blocksToDownload.ToArray(), onBlockDownloadedCallback);
            }
        }

        /// <inheritdoc />
        public async Task<ChainedHeaderBlock> GetBlockDataAsync(uint256 blockHash)
        {
            Guard.NotNull(blockHash, nameof(blockHash));

            ChainedHeaderBlock chainedHeaderBlock;

            lock (this.peerLock)
            {
                chainedHeaderBlock = this.chainedHeaderTree.GetChainedHeaderBlock(blockHash);
            }

            if (chainedHeaderBlock == null)
            {
                this.logger.LogTrace("Block hash '{0}' is not part of the tree.", blockHash);
                this.logger.LogTrace("(-)[INVALID_HASH]:null");
                return null;
            }

            if (chainedHeaderBlock.Block != null)
            {
                this.logger.LogTrace("Block pair '{0}' was found in memory.", chainedHeaderBlock);

                this.logger.LogTrace("(-)[FOUND_IN_CHT]:'{0}'", chainedHeaderBlock);
                return chainedHeaderBlock;
            }

            Block block = await this.blockStore.GetBlockAsync(blockHash).ConfigureAwait(false);
            if (block != null)
            {
                var newBlockPair = new ChainedHeaderBlock(block, chainedHeaderBlock.ChainedHeader);
                this.logger.LogTrace("Chained header block '{0}' was found in store.", newBlockPair);
                this.logger.LogTrace("(-)[FOUND_IN_BLOCK_STORE]:'{0}'", newBlockPair);
                return newBlockPair;
            }
            else
                this.logger.LogDebug("Block '{0}' was not found in block store.", blockHash);

            return chainedHeaderBlock;
        }

        /// <summary>
        /// Processes items in the <see cref="toDownloadQueue"/> and ask the block puller for blocks to download.
        /// If the tree has too many unconsumed blocks we will not ask block puller for more until some blocks are consumed.
        /// </summary>
        /// <remarks>
        /// Requests that have too many blocks will be split in batches.
        /// The amount of blocks in 1 batch to downloaded depends on the average value in <see cref="IBlockPuller.GetAverageBlockSizeBytes"/>.
        /// Should be protected by the <see cref="peerLock"/>.
        /// </remarks>
        private void ProcessDownloadQueueLocked()
        {
            while (this.toDownloadQueue.Count > 0)
            {
                int awaitingBlocksCount = this.expectedBlockSizes.Count;

                int freeSlots = MaxBlocksToAskFromPuller - awaitingBlocksCount;
                this.logger.LogTrace("{0} slots are available.", freeSlots);

                if (freeSlots < ConsumptionThresholdSlots)
                {
                    this.logger.LogTrace("(-)[NOT_ENOUGH_SLOTS]");
                    return;
                }

                long freeBytes = this.maxUnconsumedBlocksDataBytes - this.chainedHeaderTree.UnconsumedBlocksDataBytes - this.expectedBlockDataBytes;
                this.logger.LogTrace("{0} bytes worth of blocks is available for download.", freeBytes);

                if (freeBytes <= this.ConsumptionThresholdBytes)
                {
                    this.logger.LogTrace("(-)[THRESHOLD_NOT_MET]");
                    return;
                }

                // To fix issue https://github.com/stratisproject/StratisBitcoinFullNode/issues/2294#issue-364513736
                // if there are no samples, assume the worst scenario (you are going to donwload full blocks).
                long avgSize = (long)this.blockPuller.GetAverageBlockSizeBytes();
                if (avgSize == 0)
                {
                    avgSize = this.network.Consensus.Options.MaxBlockBaseSize;
                }

                int maxBlocksToAsk = Math.Min((int)(freeBytes / avgSize), freeSlots);

                this.logger.LogTrace("With {0} average block size, we have {1} download slots available.", avgSize, maxBlocksToAsk);

                BlockDownloadRequest request = this.toDownloadQueue.Peek();

                if (request.BlocksToDownload.Count <= maxBlocksToAsk)
                {
                    this.toDownloadQueue.Dequeue();
                }
                else
                {
                    this.logger.LogTrace("Splitting enqueued job of size {0} into 2 pieces of sizes {1} and {2}.", request.BlocksToDownload.Count, maxBlocksToAsk, request.BlocksToDownload.Count - maxBlocksToAsk);

                    // Split queue item in 2 pieces: one of size blocksToAsk and second is the rest. Ask BP for first part, leave 2nd part in the queue.
                    var blockPullerRequest = new BlockDownloadRequest()
                    {
                        BlocksToDownload = new List<ChainedHeader>(request.BlocksToDownload.GetRange(0, maxBlocksToAsk))
                    };

                    request.BlocksToDownload.RemoveRange(0, maxBlocksToAsk);

                    request = blockPullerRequest;
                }

                this.blockPuller.RequestBlocksDownload(request.BlocksToDownload);

                foreach (ChainedHeader chainedHeader in request.BlocksToDownload)
                    this.expectedBlockSizes.Add(chainedHeader.HashBlock, avgSize);

                this.expectedBlockDataBytes += request.BlocksToDownload.Count * avgSize;

                this.logger.LogTrace("Expected block data bytes was set to {0} and we are expecting {1} blocks to be delivered.", this.expectedBlockDataBytes, this.expectedBlockSizes.Count);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if consensus' height is within <see cref="ConsensusIsConsideredToBeSyncedMargin"/>
        /// blocks from the best tip's height.
        /// </summary>
        /// <remarks>Should be locked by <see cref="peerLock"/>.</remarks>
        private bool IsConsensusConsideredToBeSyncedLocked()
        {
            ChainedHeader bestTip = this.chainedHeaderTree.GetBestPeerTip();

            if (bestTip == null)
            {
                this.logger.LogTrace("(-)[NO_PEERS]:false");
                return false;
            }

            bool isConsideredSynced = this.Tip.Height + ConsensusIsConsideredToBeSyncedMargin > bestTip.Height;

            return isConsideredSynced;
        }

        private void AddInlineStats(StringBuilder log)
        {
            lock (this.peerLock)
            {
                ChainedHeader bestTip = this.chainedHeaderTree.GetBestPeerTip();

                if ((bestTip == null) || (bestTip.Height < this.Tip.Height))
                    bestTip = this.Tip;

                string headersLog = "Headers.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + bestTip.Height.ToString().PadRight(8) +
                                    " Headers.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + bestTip.HashBlock;

                log.AppendLine(headersLog);
            }

            string consensusLog = "Consensus.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + this.Tip.Height.ToString().PadRight(8) +
                                  " Consensus.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + this.Tip.HashBlock;

            log.AppendLine(consensusLog);
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            benchLog.AppendLine(this.performanceCounter.TakeSnapshot().ToString());
        }

        private void AddComponentStats(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("======Consensus Manager======");

            lock (this.peerLock)
            {
                if (this.isIbd) log.AppendLine("IBD Stage");

                log.AppendLine($"Chained header tree size: {this.chainedHeaderTree.ChainedBlocksDataBytes.BytesToMegaBytes()} MB");

                string unconsumedBlocks = this.FormatBigNumber(this.chainedHeaderTree.UnconsumedBlocksCount);

                double filledPercentage = Math.Round((this.chainedHeaderTree.UnconsumedBlocksDataBytes / (double)this.maxUnconsumedBlocksDataBytes) * 100, 2);

                log.AppendLine($"Unconsumed blocks: {unconsumedBlocks} -- ({this.chainedHeaderTree.UnconsumedBlocksDataBytes.BytesToMegaBytes()} / {this.maxUnconsumedBlocksDataBytes.BytesToMegaBytes()} MB). Cache is filled by: {filledPercentage}%");

                int pendingDownloadCount = this.callbacksByBlocksRequestedHash.Count;
                int currentlyDownloadingCount = this.expectedBlockSizes.Count;

                log.AppendLine($"Downloading blocks: {currentlyDownloadingCount} queued out of {pendingDownloadCount} pending");
            }
        }

        /// <summary>Formats the big number.</summary>
        /// <remarks><c>123456789</c> => <c>123,456,789</c>.</remarks>
        private string FormatBigNumber(long number)
        {
            return $"{number:#,##0}";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.reorgLock.Dispose();
        }
    }
}