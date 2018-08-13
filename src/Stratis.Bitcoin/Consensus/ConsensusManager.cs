using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus.ValidationResults;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Consensus.Visitors;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <inheritdoc cref="IConsensusManager"/>
    public class ConsensusManager : IConsensusManager
    {
        private readonly Network network;
        private readonly ILogger logger;

        internal IChainedHeaderTree ChainedHeaderTree { get; private set; }

        private readonly IChainState chainState;

        internal IPartialValidator PartialValidator { get; private set; }

        private readonly ConsensusSettings consensusSettings;
        private readonly IConsensusRuleEngine consensusRules;
        private readonly Signals.Signals signals;
        private readonly IPeerBanning peerBanning;

        internal IBlockStore BlockStore { get; private set; }

        private readonly IFinalizedBlockInfo finalizedBlockInfo;

        internal IBlockPuller BlockPuller { get; private set; }

        /// <inheritdoc />
        public ChainedHeader Tip { get; private set; }

        /// <inheritdoc />
        public IConsensusRuleEngine ConsensusRules => this.consensusRules;

        internal Dictionary<uint256, List<OnBlockDownloadedCallback>> CallbacksByBlocksRequestedHash { get; set; }

        /// <summary>Peers mapped by their ID.</summary>
        /// <remarks>This object has to be protected by <see cref="PeerLock"/>.</remarks>
        internal Dictionary<int, INetworkPeer> PeersByPeerId { get; private set; }

        internal Queue<BlockDownloadRequest> ToDownloadQueue { get; private set; }

        /// <summary>Protects access to the <see cref="BlockPuller"/>, <see cref="ChainedHeaderTree"/>, <see cref="expectedBlockSizes"/> and <see cref="expectedBlockDataBytes"/>.</summary>
        internal object PeerLock { get; private set; }

        private IInitialBlockDownloadState ibdState;

        internal object BlockRequestedLock { get; private set; }

        internal AsyncLock ReorgLock { get; private set; }

        private readonly ConcurrentChain chain;

        private bool isIbd;

        public ConsensusBlockDownloader BlockDownloader { get; private set; }

        public ConsensusBlockLoader BlockLoader { get; private set; }

        public ConsensusManager(
            Network network,
            ILoggerFactory loggerFactory,
            IChainState chainState,
            IHeaderValidator headerValidator,
            IIntegrityValidator integrityValidator,
            IPartialValidator partialValidator,
            ICheckpoints checkpoints,
            ConsensusSettings consensusSettings,
            IConsensusRuleEngine consensusRules,
            IFinalizedBlockInfo finalizedBlockInfo,
            Signals.Signals signals,
            IPeerBanning peerBanning,
            NodeSettings nodeSettings,
            IDateTimeProvider dateTimeProvider,
            IInitialBlockDownloadState ibdState,
            ConcurrentChain chain,
            IBlockPuller blockPuller,
            IBlockStore blockStore)
        {
            this.network = network;
            this.chainState = chainState;
            this.PartialValidator = partialValidator;
            this.consensusSettings = consensusSettings;
            this.consensusRules = consensusRules;
            this.signals = signals;
            this.peerBanning = peerBanning;
            this.BlockStore = blockStore;
            this.finalizedBlockInfo = finalizedBlockInfo;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.ChainedHeaderTree = new ChainedHeaderTree(network, loggerFactory, headerValidator, integrityValidator, checkpoints, chainState, finalizedBlockInfo, consensusSettings, signals);

            this.PeerLock = new object();
            this.ReorgLock = new AsyncLock();
            this.BlockRequestedLock = new object();

            this.CallbacksByBlocksRequestedHash = new Dictionary<uint256, List<OnBlockDownloadedCallback>>();
            this.PeersByPeerId = new Dictionary<int, INetworkPeer>();
            this.ToDownloadQueue = new Queue<BlockDownloadRequest>();
            this.ibdState = ibdState;

            this.BlockPuller = blockPuller;
            this.BlockDownloader = new ConsensusBlockDownloader(this, loggerFactory);
            this.BlockLoader = new ConsensusBlockLoader(loggerFactory);
        }

        /// <inheritdoc />
        /// <remarks>
        /// If <see cref="BlockStore"/> is not <c>null</c> (block store is available) then all block headers in
        /// <see cref="ChainedHeaderTree"/> will be marked as their block data is available.
        /// If store is not available the <see cref="ConsensusManager"/> won't be able to serve blocks from disk,
        /// instead all block requests that are not in memory will be sent to the <see cref="BlockPuller"/>.
        /// </remarks>
        public async Task InitializeAsync(ChainedHeader chainTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainTip), chainTip);

            // TODO: consensus store
            // We should consider creating a consensus store class that will internally contain
            // coinview and it will abstract the methods `RewindAsync()` `GetBlockHashAsync()`

            uint256 consensusTipHash = await this.consensusRules.GetBlockHashAsync().ConfigureAwait(false);
            bool blockStoreDisabled = this.BlockStore == null;

            while (true)
            {
                this.Tip = chainTip.FindAncestorOrSelf(consensusTipHash);

                if ((this.Tip != null) && (blockStoreDisabled || (this.chainState.BlockStoreTip.Height >= this.Tip.Height)))
                    break;

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                RewindState transitionState = await this.consensusRules.RewindAsync().ConfigureAwait(false);
                consensusTipHash = transitionState.BlockHash;
            }

            this.chainState.ConsensusTip = this.Tip;

            this.ChainedHeaderTree.Initialize(this.Tip, this.BlockStore != null);

            this.BlockPuller.Initialize(this.BlockDownloader.BlockDownloaded);

            this.isIbd = this.ibdState.IsInitialBlockDownload();
            this.BlockPuller.OnIbdStateChanged(this.isIbd);

            this.logger.LogTrace("(-)");
        }

        public async Task<T> AcceptVisitorAsync<T>(IConsensusVisitor<T> visitor)
        {
            return await visitor.VisitAsync(this);
        }

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="PeerLock"/>.</remarks>
        internal void PeerDisconnectedLocked(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            bool removed = this.PeersByPeerId.Remove(peerId);

            if (removed)
            {
                this.ChainedHeaderTree.PeerDisconnected(peerId);
                this.BlockPuller.PeerDisconnected(peerId);
                this.BlockDownloader.ProcessDownloadQueueLocked();
            }
            else
                this.logger.LogTrace("Peer {0} was already removed.", peerId);

            this.logger.LogTrace("(-)");
        }

        internal async Task OnPartialValidationCompletedCallbackAsync(PartialValidationResult validationResult)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(validationResult), validationResult);

            if (validationResult.Succeeded)
            {
                await this.OnPartialValidationSucceededAsync(validationResult.ChainedHeaderBlock).ConfigureAwait(false);
            }
            else
            {
                var peersToBan = new List<INetworkPeer>();

                lock (this.PeerLock)
                {
                    List<int> peerIdsToBan = this.ChainedHeaderTree.PartialOrFullValidationFailed(validationResult.ChainedHeaderBlock.ChainedHeader);

                    this.logger.LogDebug("Validation of block '{0}' failed, banning and disconnecting {1} peers.", validationResult.ChainedHeaderBlock, peerIdsToBan.Count);

                    foreach (int peerId in peerIdsToBan)
                    {
                        if (this.PeersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                            peersToBan.Add(peer);
                    }
                }

                foreach (INetworkPeer peer in peersToBan)
                    this.peerBanning.BanAndDisconnectPeer(peer.RemoteSocketEndpoint, validationResult.BanDurationSeconds, validationResult.BanReason);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Handles a situation when partial validation of a block was successful. Informs CHT about
        /// finishing partial validation process and starting a new partial validation or full validation.
        /// </summary>
        /// <param name="chainedHeaderBlock">Block which validation was successful.</param>
        private async Task OnPartialValidationSucceededAsync(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            List<ChainedHeaderBlock> chainedHeaderBlocksToValidate;
            ConnectBlocksResult connectBlocksResult = null;

            using (await this.ReorgLock.LockAsync().ConfigureAwait(false))
            {
                bool fullValidationRequired;

                lock (this.PeerLock)
                {
                    chainedHeaderBlocksToValidate = this.ChainedHeaderTree.PartialValidationSucceeded(chainedHeaderBlock.ChainedHeader, out fullValidationRequired);
                }

                this.logger.LogTrace("Full validation is{0} required.", fullValidationRequired ? "" : " NOT");

                if (fullValidationRequired)
                {
                    connectBlocksResult = await this.FullyValidateLockedAsync(chainedHeaderBlock).ConfigureAwait(false);
                }
            }

            if (connectBlocksResult != null)
            {
                if (connectBlocksResult.PeersToBan != null)
                {
                    var peersToBan = new List<INetworkPeer>();

                    lock (this.PeerLock)
                    {
                        foreach (int peerId in connectBlocksResult.PeersToBan)
                        {
                            if (this.PeersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                                peersToBan.Add(peer);
                        }
                    }

                    this.logger.LogTrace("{0} peers will be banned.", peersToBan.Count);

                    foreach (INetworkPeer peer in peersToBan)
                        this.peerBanning.BanAndDisconnectPeer(peer.PeerEndPoint, connectBlocksResult.BanDurationSeconds, connectBlocksResult.BanReason);
                }

                if (connectBlocksResult.ConsensusTipChanged)
                    await this.NotifyBehaviorsOnConsensusTipChangedAsync().ConfigureAwait(false);

                lock (this.PeerLock)
                {
                    this.BlockDownloader.ProcessDownloadQueueLocked();
                }
            }

            if (chainedHeaderBlocksToValidate != null)
            {
                this.logger.LogTrace("Partial validation of {0} block will be started.", chainedHeaderBlocksToValidate.Count);

                // Start validating all next blocks that come after the current block,
                // all headers in this list have the blocks present in the header.
                foreach (ChainedHeaderBlock toValidate in chainedHeaderBlocksToValidate)
                    this.PartialValidator.StartPartialValidation(toValidate, this.OnPartialValidationCompletedCallbackAsync);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Notifies the chained header behaviors of all connected peers when a consensus tip is changed.
        /// Consumes headers from their caches if there are any.
        /// </summary>
        private async Task NotifyBehaviorsOnConsensusTipChangedAsync()
        {
            this.logger.LogTrace("()");

            var behaviors = new List<ConsensusManagerBehavior>();

            lock (this.PeerLock)
            {
                foreach (INetworkPeer peer in this.PeersByPeerId.Values)
                    behaviors.Add(peer.Behavior<ConsensusManagerBehavior>());
            }

            var blocksToDownload = new List<ConnectNewHeadersResult>();

            foreach (ConsensusManagerBehavior consensusManagerBehavior in behaviors)
            {
                ConnectNewHeadersResult connectNewHeadersResult = await consensusManagerBehavior.ConsensusTipChangedAsync(this.Tip).ConfigureAwait(false);

                int? peerId = consensusManagerBehavior.AttachedPeer?.Connection?.Id;

                if (peerId == null)
                    continue;

                if (connectNewHeadersResult == null)
                {
                    this.logger.LogTrace("No new blocks to download were presented by peer ID {0}.", peerId);
                    continue;
                }

                blocksToDownload.Add(connectNewHeadersResult);
                this.logger.LogTrace("{0} headers were added to download by peer ID {1}.", connectNewHeadersResult.DownloadTo.Height - connectNewHeadersResult.DownloadFrom.Height + 1, peerId);
            }

            foreach (ConnectNewHeadersResult newHeaders in blocksToDownload)
                this.BlockDownloader.DownloadBlocks(newHeaders.ToArray(), this.BlockDownloader.ProcessDownloadedBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Attempt to switch to new chain, which may require rewinding blocks from the current chain.</summary>
        /// <remarks>
        /// It is possible that during connection we find out that blocks that we tried to connect are invalid and we switch back to original chain.
        /// Switching that requires rewinding may fail in case rewind goes beyond fork point and the block data is not available to advance to the fork point.
        /// </remarks>
        /// <param name="proposedNewTip">Tip of the chain that will become the tip of our consensus chain if full validation will succeed.</param>
        /// <returns>Validation related information.</returns>
        internal async Task<ConnectBlocksResult> FullyValidateLockedAsync(ChainedHeaderBlock proposedNewTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(proposedNewTip), proposedNewTip);

            ChainedHeader oldTip = this.Tip;
            ChainedHeader newTip = proposedNewTip.ChainedHeader;

            ChainedHeader fork = oldTip.FindFork(newTip);

            if (fork == newTip)
            {
                // The new header is behind the current tip this is a bug.
                this.logger.LogError("New header '{0}' is behind the current tip '{1}'.", newTip, oldTip);
                this.logger.LogTrace("(-)[INVALID_NEW_TIP]");
                throw new ConsensusException("New tip must be ahead of old tip.");
            }

            ChainedHeader currentTip = fork;

            // If the new block is not on the current chain as our current consensus tip
            // then rewind consensus tip to the common fork (or earlier because rewind might jump a few blocks back).
            bool isExtension = fork == oldTip;

            if (!isExtension)
                currentTip = await this.RewindToForkPointOrBelowAsync(fork, oldTip).ConfigureAwait(false);

            List<ChainedHeaderBlock> blocksToConnect = await this.TryGetBlocksToConnectAsync(newTip, currentTip.Height + 1).ConfigureAwait(false);

            if (blocksToConnect == null)
            {
                // In a situation where the rewind operation ended up behind fork point we may end up with a gap with missing blocks (if the reorg is big enough)
                // In that case we try to load the blocks from store, if store is not present we disconnect all peers.
                this.HandleMissingBlocksGap(currentTip);

                var result = new ConnectBlocksResult(false);
                this.logger.LogTrace("(-)[GAP_BEFORE_CONNECTING]:'{0}'", result);
                return result;
            }

            ConnectBlocksResult connectBlockResult = await this.ConnectChainAsync(newTip, blocksToConnect).ConfigureAwait(false);

            if (connectBlockResult.Succeeded)
            {
                this.logger.LogTrace("(-)[SUCCEEDED]:'{0}'", connectBlockResult);
                return connectBlockResult;
            }

            if (connectBlockResult.LastValidatedBlockHeader != null)
            {
                // Block validation failed we need to rewind any blocks that were added to the chain.
                await this.RewindPartiallyConnectedChainAsync(connectBlockResult.LastValidatedBlockHeader, currentTip).ConfigureAwait(false);
            }

            if (isExtension)
            {
                this.logger.LogTrace("(-)[DIDNT_REWIND]:'{0}'", connectBlockResult);
                return connectBlockResult;
            }

            List<ChainedHeaderBlock> blocksToReconnect = await this.TryGetBlocksToConnectAsync(oldTip, currentTip.Height + 1).ConfigureAwait(false);

            if (blocksToReconnect == null)
            {
                // We tried to reapply old chain but we don't have all the blocks to do that.
                this.HandleMissingBlocksGap(currentTip);

                var result = new ConnectBlocksResult(false);
                this.logger.LogTrace("(-)[GAP_AFTER_CONNECTING]:'{0}'", result);
                return result;
            }

            ConnectBlocksResult reconnectionResult = await this.ReconnectOldChainAsync(currentTip, blocksToReconnect).ConfigureAwait(false);

            this.logger.LogTrace("(-):'{0}'", reconnectionResult);
            return reconnectionResult;
        }

        /// <summary>Rewinds to fork point or below it.</summary>
        /// <returns>New consensus tip.</returns>
        private async Task<ChainedHeader> RewindToForkPointOrBelowAsync(ChainedHeader fork, ChainedHeader oldTip)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}'", nameof(fork), fork, nameof(oldTip), oldTip);

            ChainedHeader currentTip = oldTip;

            while (fork.Height < currentTip.Height)
            {
                RewindState transitionState = await this.consensusRules.RewindAsync().ConfigureAwait(false);

                lock (this.PeerLock)
                {
                    currentTip = this.ChainedHeaderTree.GetChainedHeader(transitionState.BlockHash);
                }
            }

            this.logger.LogTrace("(-):'{0}'", currentTip);
            return currentTip;
        }

        /// <summary>Rewinds the connected part of invalid chain.</summary>
        private async Task RewindPartiallyConnectedChainAsync(ChainedHeader lastValidatedBlockHeader, ChainedHeader currentTip)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}'", nameof(lastValidatedBlockHeader), lastValidatedBlockHeader, nameof(currentTip), currentTip);

            ChainedHeader current = lastValidatedBlockHeader;

            while (currentTip.Height < current.Height)
            {
                RewindState transitionState = await this.consensusRules.RewindAsync().ConfigureAwait(false);
                lock (this.PeerLock)
                {
                    current = this.ChainedHeaderTree.GetChainedHeader(transitionState.BlockHash);
                }
            }

            if (currentTip.Height != current.Height)
            {
                // The rewind operation must return to the same fork point.
                this.logger.LogError("The rewind operation ended up at '{0}' instead of fork point '{1}'.", currentTip, current);
                this.logger.LogTrace("(-)[INVALID_REWIND]");
                throw new ConsensusException("The rewind operation must return to the same fork point.");
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Connects new chain.</summary>
        /// <param name="newTip">New tip.</param>
        /// <param name="blocksToConnect">List of blocks to connect.</param>
        private async Task<ConnectBlocksResult> ConnectChainAsync(ChainedHeader newTip, List<ChainedHeaderBlock> blocksToConnect)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(newTip), newTip, nameof(blocksToConnect), nameof(blocksToConnect.Count), blocksToConnect.Count);

            ChainedHeader lastValidatedBlockHeader = null;
            ConnectBlocksResult connectBlockResult = null;

            foreach (ChainedHeaderBlock blockToConnect in blocksToConnect)
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
                List<int> peersToResync = this.SetConsensusTip(newTip);

                await this.ResyncPeersAsync(peersToResync).ConfigureAwait(false);

                if (this.network.Consensus.MaxReorgLength != 0)
                {
                    int newFinalizedHeight = newTip.Height - (int)this.network.Consensus.MaxReorgLength;

                    if (newFinalizedHeight > 0)
                    {
                        uint256 newFinalizedHash = newTip.GetAncestor(newFinalizedHeight).HashBlock;

                        await this.finalizedBlockInfo.SaveFinalizedBlockHashAndHeightAsync(newFinalizedHash, newFinalizedHeight).ConfigureAwait(false);
                    }
                }

                // TODO: change signal to take ChainedHeaderBlock
                this.signals.SignalBlockConnected(blockToConnect.Block);
            }

            this.logger.LogTrace("(-):'{0}'", connectBlockResult);
            return connectBlockResult;
        }

        /// <summary>Reconnects the old chain.</summary>
        /// <param name="currentTip">Current tip.</param>
        /// <param name="blocksToReconnect">List of blocks to reconnect.</param>
        private async Task<ConnectBlocksResult> ReconnectOldChainAsync(ChainedHeader currentTip, List<ChainedHeaderBlock> blocksToReconnect)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(currentTip), currentTip, nameof(blocksToReconnect), nameof(blocksToReconnect.Count), blocksToReconnect.Count);

            // Connect back the old blocks.
            ConnectBlocksResult connectBlockResult = await this.ConnectChainAsync(currentTip, blocksToReconnect).ConfigureAwait(false);

            if (connectBlockResult.Succeeded)
            {
                var result = new ConnectBlocksResult(false, false);
                this.logger.LogTrace("(-):'{0}'", result);
                return result;
            }

            // We failed to jump back on the previous chain after a failed reorg.
            // And we failed to reconnect the old chain, database might be corrupted.
            this.logger.LogError("A critical error has prevented reconnecting blocks");
            this.logger.LogTrace("(-)[FAILED_TO_RECONNECT]");
            throw new ConsensusException("A critical error has prevented reconnecting blocks.");
        }

        /// <summary>Disconnects all the peers and sets the consensus tip to specified value.</summary>
        /// <remarks>
        /// In case we failed to retrieve blocks from any of the storages that we have during the process of consensus tip switching we want to disconnect
        /// from all peers and reset consensus tip before the fork point between two chains (one that is ours and another which we tried switch to).
        /// Disconnection is needed to avoid having CHT in an inconsistent state and to increase the probability of connecting to a new set of peers
        /// which claims the same chain because they had enough time to handle the chain split.
        /// </remarks>
        /// <param name="newTip">The new tip.</param>
        private void HandleMissingBlocksGap(ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            List<INetworkPeer> peers;

            lock (this.PeerLock)
            {
                peers = this.PeersByPeerId.Values.ToList();

                this.logger.LogTrace("Simulating disconnection for {0} peers.", peers.Count);

                // First make sure headers are removed from CHT by emulating peers disconnection.
                foreach (INetworkPeer networkPeer in peers)
                    this.PeerDisconnectedLocked(networkPeer.Connection.Id);

                this.SetConsensusTipLocked(newTip);
            }

            // Actually disconnect the peers.
            foreach (INetworkPeer networkPeer in peers)
                networkPeer.Disconnect("Consensus out of sync.");

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Informs <see cref="ConsensusManagerBehavior"/> of each peer
        /// to be resynced and simulates disconnection of the peer.
        /// </summary>
        /// <param name="peerIds">List of peer IDs to resync.</param>
        private async Task ResyncPeersAsync(List<int> peerIds)
        {
            this.logger.LogTrace("{0}.{1}:{2}", nameof(peerIds), nameof(peerIds.Count), peerIds.Count);

            var resyncTasks = new List<Task>(peerIds.Count);

            lock (this.PeerLock)
            {
                foreach (int peerId in peerIds)
                {
                    if (this.PeersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Attempts to connect a block to a chain with specified tip.
        /// </summary>
        /// <param name="blockToConnect">Block to connect.</param>
        /// <exception cref="ConsensusException">Thrown in case CHT is not in a consistent state.</exception>
        private async Task<ConnectBlocksResult> ConnectBlockAsync(ChainedHeaderBlock blockToConnect)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockToConnect), blockToConnect);

            if ((blockToConnect.ChainedHeader.BlockValidationState != ValidationState.PartiallyValidated) &&
                (blockToConnect.ChainedHeader.BlockValidationState != ValidationState.FullyValidated))
            {
                this.logger.LogError("Block '{0}' must be partially or fully validated but it is {1}.", blockToConnect, blockToConnect.ChainedHeader.BlockValidationState);
                this.logger.LogTrace("(-)[BLOCK_INVALID_STATE]");
                throw new ConsensusException("Block must be partially or fully validated.");
            }

            var validationContext = new ValidationContext() { Block = blockToConnect.Block, ChainTipToExtend = blockToConnect.ChainedHeader };

            // Call the validation engine.
            await this.consensusRules.FullValidationAsync(validationContext).ConfigureAwait(false);

            if (validationContext.Error != null)
            {
                List<int> badPeers;

                lock (this.PeerLock)
                {
                    badPeers = this.ChainedHeaderTree.PartialOrFullValidationFailed(blockToConnect.ChainedHeader);
                }

                var failureResult = new ConnectBlocksResult(false, false, badPeers, validationContext.Error.Message, validationContext.BanDurationSeconds);

                this.logger.LogTrace("(-)[FAILED]:'{0}'", failureResult);
                return failureResult;
            }

            lock (this.PeerLock)
            {
                this.ChainedHeaderTree.FullValidationSucceeded(blockToConnect.ChainedHeader);

                this.chainState.IsAtBestChainTip = this.ChainedHeaderTree.IsAtBestChainTip();
            }

            var result = new ConnectBlocksResult(true);

            this.logger.LogTrace("(-):'{0}'", result);
            return result;
        }

        /// <summary>Try to find all blocks between two headers.</summary>
        /// <returns>Collection of blocks that were loaded. In case at least one block was not present <c>null</c> will be returned.</returns>
        private async Task<List<ChainedHeaderBlock>> TryGetBlocksToConnectAsync(ChainedHeader proposedNewTip, int heightOfFirstBlock)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(proposedNewTip), proposedNewTip, nameof(heightOfFirstBlock), heightOfFirstBlock);

            ChainedHeader currentHeader = proposedNewTip;
            var chainedHeaderBlocks = new List<ChainedHeaderBlock>();

            while (currentHeader.Height >= heightOfFirstBlock)
            {
                ChainedHeaderBlock chainedHeaderBlock = await this.BlockLoader.LoadBlockDataAsync(this, currentHeader.HashBlock).ConfigureAwait(false);

                if (chainedHeaderBlock == null)
                {
                    this.logger.LogTrace("(-):null");
                    return null;
                }

                chainedHeaderBlocks.Add(chainedHeaderBlock);
                currentHeader = currentHeader.Previous;
            }

            this.logger.LogTrace("(-):{0}:'{1}'", nameof(chainedHeaderBlocks), chainedHeaderBlocks.Count);
            return chainedHeaderBlocks;
        }

        private List<int> SetConsensusTip(ChainedHeader newTip)
        {
            lock (this.PeerLock)
            {
                return this.SetConsensusTipLocked(newTip);
            }
        }

        /// <summary>Sets the consensus tip.</summary>
        /// <remarks>Have to be locked by <see cref="PeerLock"/>.</remarks>
        /// <param name="newTip">New consensus tip.</param>
        private List<int> SetConsensusTipLocked(ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            List<int> peerIdsToResync = this.ChainedHeaderTree.ConsensusTipChanged(newTip);

            this.Tip = newTip;

            this.chainState.ConsensusTip = this.Tip;
            this.chain.SetTip(this.Tip);

            bool ibd = this.ibdState.IsInitialBlockDownload();

            if (ibd != this.isIbd)
                this.BlockPuller.OnIbdStateChanged(ibd);

            this.isIbd = ibd;

            this.logger.LogTrace("(-):*.{0}={1}", nameof(peerIdsToResync.Count), peerIdsToResync.Count);
            return peerIdsToResync;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.ReorgLock.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}