using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling2;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusManager : IDisposable
    {
        /// <summary>
        /// Maximum memory in bytes that can be taken by the blocks that were downloaded but
        /// not yet validated or included to the consensus chain.
        /// </summary>
        private const long MaxUnconsumedBlocksDataBytes = 200 * 1024 * 1024;

        /// <summary>Queue consumption threshold in bytes.</summary>
        /// <remarks><see cref="toDownloadQueue"/> consumption will start if only we have more than this value of free memory.</remarks>
        private const long ConsumptionThresholdBytes = MaxUnconsumedBlocksDataBytes / 10;

        /// <summary>The default number of blocks to ask when there is no historic data to estimate average block size.</summary>
        private const int DefaultNumberOfBlocksToAsk = 10;

        private readonly Network network;
        private readonly ILogger logger;
        private readonly IChainedHeaderTree chainedHeaderTree;
        private readonly IChainState chainState;
        private readonly IBlockValidator blockValidator;
        private readonly ConsensusSettings consensusSettings;
        private readonly IBlockPuller blockPuller;
        private readonly IConsensusRules consensusRules;
        private readonly Signals.Signals signals;
        private readonly IPeerBanning peerBanning;
        private readonly IBlockStore blockStore;
        private readonly IFinalizedBlockHeight finalizedBlockHeight;

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader Tip { get; private set; }

        private readonly Dictionary<uint256, List<OnBlockDownloadedCallback>> callbacksByBlocksRequestedHash;

        /// <summary>Peers mapped by their ID.</summary>
        /// <remarks>This object has to be protected by <see cref="peerLock"/>.</remarks>
        private readonly Dictionary<int, INetworkPeer> peersByPeerId;

        private readonly Queue<BlockDownloadRequest> toDownloadQueue;

        /// <summary>Protects access to the <see cref="blockPuller"/>, <see cref="chainedHeaderTree"/>, <see cref="expectedBlockSizes"/> and <see cref="expectedBlockDataBytes"/>.</summary>
        private readonly object peerLock;

        /// <inheritdoc cref="IInitialBlockDownloadState"/>
        private IInitialBlockDownloadState ibdState;

        private readonly object blockRequestedLock;

        private readonly AsyncLock reorgLock;

        private long expectedBlockDataBytes;

        private readonly Dictionary<uint256, long> expectedBlockSizes;

        private readonly ConcurrentChain chain;

        private bool isIbd;

        public ConsensusManager(
            Network network,
            ILoggerFactory loggerFactory,
            IChainState chainState,
            IBlockValidator blockValidator,
            ICheckpoints checkpoints,
            ConsensusSettings consensusSettings,
            IConsensusRules consensusRules,
            IFinalizedBlockHeight finalizedBlockHeight,
            Signals.Signals signals,
            IPeerBanning peerBanning,
            NodeSettings nodeSettings,
            IDateTimeProvider dateTimeProvider,
            IInitialBlockDownloadState ibdState,
            ConcurrentChain chain,
            IBlockStore blockStore = null)
        {
            this.network = network;
            this.chainState = chainState;
            this.blockValidator = blockValidator;
            this.consensusSettings = consensusSettings;
            this.consensusRules = consensusRules;
            this.signals = signals;
            this.peerBanning = peerBanning;
            this.blockStore = blockStore;
            this.finalizedBlockHeight = finalizedBlockHeight;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.chainedHeaderTree = new ChainedHeaderTree(network, loggerFactory, blockValidator, checkpoints, chainState, finalizedBlockHeight, consensusSettings);

            this.peerLock = new object();
            this.reorgLock = new AsyncLock();
            this.blockRequestedLock = new object();
            this.expectedBlockDataBytes = 0;
            this.expectedBlockSizes = new Dictionary<uint256, long>();

            this.callbacksByBlocksRequestedHash = new Dictionary<uint256, List<OnBlockDownloadedCallback>>();
            this.peersByPeerId = new Dictionary<int, INetworkPeer>();
            this.toDownloadQueue = new Queue<BlockDownloadRequest>();
            this.ibdState = ibdState;

            ProtocolVersion protocolVersion = nodeSettings.ProtocolVersion;

            this.blockPuller = new BlockPuller(this.BlockDownloaded, this.chainState, protocolVersion, dateTimeProvider, loggerFactory);
        }

        /// <summary>
        /// Set the tip of <see cref="ConsensusManager"/>, if the given <paramref name="chainTip"/> is not equal to <see cref="Tip"/>
        /// then rewind consensus until a common header is found.
        /// </summary>
        /// <remarks>
        /// If <see cref="blockStore"/> is not <c>null</c> (block store is available) then all block headers in
        /// <see cref="chainedHeaderTree"/> will be marked as their block data is available.
        /// If store is not available the <see cref="ConsensusManager"/> won't be able to serve blocks from disk,
        /// instead all block requests that are not in memory will be sent to the <see cref="blockPuller"/>.
        /// </remarks>
        /// <param name="chainTip">Last common header between chain repository and block store if it's available,
        /// if the store is not available it is the chain repository tip.</param>
        public async Task InitializeAsync(ChainedHeader chainTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainTip), chainTip);

            // TODO: consensus store
            // We should consider creating a consensus store class that will internally contain
            //  coinview and it will abstract the methods `RewindAsync()` `GetBlockHashAsync()`

            uint256 consensusTipHash = await this.consensusRules.GetBlockHashAsync().ConfigureAwait(false);

            while (true)
            {
                this.Tip = chainTip.FindAncestorOrSelf(consensusTipHash);

                if (this.Tip?.HashBlock == consensusTipHash)
                    break;

                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                RewindState transitionState = await this.consensusRules.RewindAsync().ConfigureAwait(false);
                consensusTipHash = transitionState.BlockHash;
            }

            this.chainState.ConsensusTip = this.Tip;

            this.chainedHeaderTree.Initialize(this.Tip, this.blockStore != null);

            this.blockPuller.Initialize();

            this.isIbd = this.ibdState.IsInitialBlockDownload();
            this.blockPuller.OnIbdStateChanged(this.isIbd);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A list of headers are presented from a given peer,
        /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
        /// </summary>
        /// <param name="peer">The peer that providing the headers.</param>
        /// <param name="headers">The list of new headers.</param>
        /// <param name="triggerDownload">Specifies if the download should be scheduled for interesting blocks.</param>
        /// <returns>Information about consumed headers.</returns>
        /// <exception cref="ConnectHeaderException">Thrown when first presented header can't be connected to any known chain in the tree.</exception>
        /// <exception cref="CheckpointMismatchException">Thrown if checkpointed header doesn't match the checkpoint hash.</exception>
        public ConnectNewHeadersResult HeadersPresented(INetworkPeer peer, List<BlockHeader> headers, bool triggerDownload = true)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4},{5}:{6})", nameof(peer.Connection.Id), peer.Connection.Id, nameof(headers), nameof(headers.Count), headers.Count, nameof(triggerDownload), triggerDownload);

            ConnectNewHeadersResult connectNewHeadersResult;

            lock (this.peerLock)
            {
                int peerId = peer.Connection.Id;

                connectNewHeadersResult = this.chainedHeaderTree.ConnectNewHeaders(peerId, headers);
                this.blockPuller.NewPeerTipClaimed(peer, connectNewHeadersResult.Consumed);

                if (!this.peersByPeerId.ContainsKey(peerId))
                {
                    this.peersByPeerId.Add(peerId, peer);
                    this.logger.LogTrace("New peer with ID {0} was added.", peerId);
                }
            }

            if (triggerDownload && (connectNewHeadersResult.DownloadTo != null))
                this.DownloadBlocks(connectNewHeadersResult.ToArray(), this.ProcessDownloadedBlock);

            this.logger.LogTrace("(-):'{0}'", connectNewHeadersResult);
            return connectNewHeadersResult;
        }

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <param name="peerId">The peer that was disconnected.</param>
        public void PeerDisconnected(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            lock (this.peerLock)
            {
                this.PeerDisconnectedLocked(peerId);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="peerLock"/>.</remarks>
        /// <param name="peerId">The peer that was disconnected.</param>
        private void PeerDisconnectedLocked(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            bool removed = this.peersByPeerId.Remove(peerId);

            if (removed)
            {
                this.chainedHeaderTree.PeerDisconnected(peerId);
                this.blockPuller.PeerDisconnected(peerId);
                this.ProcessDownloadQueueLocked();
            }
            else
                this.logger.LogTrace("Peer {0} was already removed.", peerId);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// A callback that is triggered when a block that <see cref="ConsensusManager"/> requested was downloaded.
        /// </summary>
        private void ProcessDownloadedBlock(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            bool partialValidationRequired = false;

            lock (this.peerLock)
            {
                partialValidationRequired = this.chainedHeaderTree.BlockDataDownloaded(chainedHeaderBlock.ChainedHeader, chainedHeaderBlock.Block);
            }

            if (partialValidationRequired)
                this.blockValidator.StartPartialValidation(chainedHeaderBlock, this.OnPartialValidationCompletedCallbackAsync);

            this.logger.LogTrace("(-)");
        }

        private async Task OnPartialValidationCompletedCallbackAsync(PartialValidationResult validationResult)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(validationResult), validationResult);

            if (validationResult.Succeeded)
            {
                await this.OnPartialValidationSucceededAsync(validationResult.ChainedHeaderBlock).ConfigureAwait(false);
            }
            else
            {
                var peersToBan = new List<INetworkPeer>();

                lock (this.peerLock)
                {
                    List<int> peerIdsToBan = this.chainedHeaderTree.PartialOrFullValidationFailed(validationResult.ChainedHeaderBlock.ChainedHeader);

                    this.logger.LogDebug("Validation of block '{0}' failed, banning and disconnecting {1} peers.", validationResult.ChainedHeaderBlock, peerIdsToBan.Count);

                    foreach (int peerId in peerIdsToBan)
                    {
                        if (this.peersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
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

            using (await this.reorgLock.LockAsync().ConfigureAwait(false))
            {
                bool fullValidationRequired;

                lock (this.peerLock)
                {
                    chainedHeaderBlocksToValidate = this.chainedHeaderTree.PartialValidationSucceeded(chainedHeaderBlock.ChainedHeader, out fullValidationRequired);
                }

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
                    this.NotifyBehaviorsOnConsensusTipChanged();

                lock (this.peerLock)
                {
                    this.ProcessDownloadQueueLocked();
                }
            }

            if (chainedHeaderBlocksToValidate != null)
            {
                this.logger.LogTrace("Partial validation of {0} block will be started.", chainedHeaderBlocksToValidate.Count);

                // Start validating all next blocks that come after the current block,
                // all headers in this list have the blocks present in the header.
                foreach (ChainedHeaderBlock toValidate in chainedHeaderBlocksToValidate)
                    this.blockValidator.StartPartialValidation(toValidate, this.OnPartialValidationCompletedCallbackAsync);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Notifies the chained header behaviors of all connected peers when a consensus tip is changed.
        /// Consumes headers from their caches if there are any.
        /// </summary>
        private void NotifyBehaviorsOnConsensusTipChanged()
        {
            this.logger.LogTrace("()");

            var behaviors = new List<ChainHeadersBehavior>();

            lock (this.peerLock)
            {
                foreach (INetworkPeer peer in this.peersByPeerId.Values)
                    behaviors.Add(peer.Behavior<ChainHeadersBehavior>());
            }

            var blocksToDownload = new List<ConnectNewHeadersResult>();

            foreach (ChainHeadersBehavior chainHeadersBehavior in behaviors)
            {
                ConnectNewHeadersResult connectNewHeadersResult = chainHeadersBehavior.ConsensusTipChanged(this.Tip);

                int? peerId = chainHeadersBehavior.AttachedPeer?.Connection?.Id;

                if (peerId == null)
                    continue;

                if (connectNewHeadersResult.DownloadTo == null)
                {
                    this.logger.LogTrace("No new blocks to download were presented by peer ID {0}.", peerId);
                    continue;
                }

                blocksToDownload.Add(connectNewHeadersResult);
                this.logger.LogTrace("{0} headers were added to download by peer ID {1}.", connectNewHeadersResult.DownloadTo.Height - connectNewHeadersResult.DownloadFrom.Height + 1, peerId);
            }

            foreach (ConnectNewHeadersResult newHeaders in blocksToDownload)
                this.DownloadBlocks(newHeaders.ToArray(), this.ProcessDownloadedBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Attempt to switch to new chain, which may require rewinding blocks from the current chain.</summary>
        /// <remarks>
        /// It is possible that during connection we find out that blocks that we tried to connect are invalid and we switch back to original chain.
        /// Switching that requires rewinding may fail in case rewind goes beyond fork point and the block data is not available to advance to the fork point.
        /// </remarks>
        /// <param name="proposedNewTip">Tip of the chain that will become the tip of our consensus chain if full validation will succeed.</param>
        /// <returns>Validation related information.</returns>
        private async Task<ConnectBlocksResult> FullyValidateLockedAsync(ChainedHeaderBlock proposedNewTip)
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

            ConnectBlocksResult connectBlockResult = await this.ConnectNewChainAsync(newTip, currentTip, blocksToConnect).ConfigureAwait(false);

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

            ConnectBlocksResult reconnectionResult = await this.ReconnectOldChainAsync(oldTip, currentTip, blocksToReconnect).ConfigureAwait(false);

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

                lock (this.peerLock)
                {
                    currentTip = this.chainedHeaderTree.GetChainedHeader(transitionState.BlockHash);
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
                lock (this.peerLock)
                {
                    current = this.chainedHeaderTree.GetChainedHeader(transitionState.BlockHash);
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
        /// <param name="currentTip">Current tip.</param>
        /// <param name="blocksToConnect">List of blocks to connect.</param>
        private async Task<ConnectBlocksResult> ConnectNewChainAsync(ChainedHeader newTip, ChainedHeader currentTip, List<ChainedHeaderBlock> blocksToConnect)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}.{5}:{6})", nameof(newTip), newTip, nameof(currentTip), currentTip, nameof(blocksToConnect), nameof(blocksToConnect.Count), blocksToConnect.Count);

            ConnectBlocksResult connectBlockResult = await this.ConnectBlocksAsync(currentTip, blocksToConnect).ConfigureAwait(false);

            if (!connectBlockResult.Succeeded)
            {
                this.logger.LogTrace("(-)[FAILED_TO_CONNECT]:'{0}'", connectBlockResult);
                return connectBlockResult;
            }

            // Blocks connected successfully.
            List<int> peersToResync = this.SetConsensusTip(newTip);

            await this.ResyncPeersAsync(peersToResync).ConfigureAwait(false);

            if (this.network.Consensus.MaxReorgLength != 0)
            {
                int newFinalizedHeight = newTip.Height - (int)this.network.Consensus.MaxReorgLength;

                await this.finalizedBlockHeight.SaveFinalizedBlockHeightAsync(newFinalizedHeight).ConfigureAwait(false);
            }

            foreach (ChainedHeaderBlock validatedBlock in blocksToConnect)
            {
                // TODO: change signal to take ChainedHeaderBlock
                this.signals.SignalBlock(validatedBlock.Block);
            }

            this.logger.LogTrace("(-):'{0}'", connectBlockResult);
            return connectBlockResult;
        }

        /// <summary>Reconnects the old chain.</summary>
        /// <param name="oldTip">The old tip.</param>
        /// <param name="currentTip">Current tip.</param>
        /// <param name="blocksToReconnect">List of blocks to reconnect.</param>
        private async Task<ConnectBlocksResult> ReconnectOldChainAsync(ChainedHeader oldTip, ChainedHeader currentTip, List<ChainedHeaderBlock> blocksToReconnect)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}.{5}:{6})", nameof(oldTip), oldTip, nameof(currentTip), currentTip, nameof(blocksToReconnect), nameof(blocksToReconnect.Count), blocksToReconnect.Count);

            // Connect back the old blocks.
            ConnectBlocksResult connectBlockResult = await this.ConnectBlocksAsync(currentTip, blocksToReconnect).ConfigureAwait(false);

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
        /// Disconnection is needed to avoid having CHT in an inconsistent that and to have a high probability of connecting to a new set of peers
        /// which claims the same chain because they had enough time to handle the chain split.
        /// </remarks>
        /// <param name="newTip">The new tip.</param>
        private void HandleMissingBlocksGap(ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            List<INetworkPeer> peers;

            lock (this.peerLock)
            {
                peers = this.peersByPeerId.Values.ToList();

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
        /// Informs <see cref="ChainHeadersBehavior"/> of each peer
        /// to be resynced and simulates disconnection of the peer.
        /// </summary>
        /// <param name="peerIds">List of peer IDs to resync.</param>
        private async Task ResyncPeersAsync(List<int> peerIds)
        {
            this.logger.LogTrace("{0}.{1}:{2}", nameof(peerIds), nameof(peerIds.Count), peerIds.Count);

            var resyncTasks = new List<Task>(peerIds.Count);

            lock (this.peerLock)
            {
                foreach (int peerId in peerIds)
                {
                    if (this.peersByPeerId.TryGetValue(peerId, out INetworkPeer peer))
                    {
                        this.logger.LogTrace("Resyncing peer ID {0}.", peerId);

                        Task task = peer.Behavior<ChainHeadersBehavior>().ResetPendingTipAndSyncAsync();
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
        /// Attempts to connect a chain of blocks to a chain with specified tip.
        /// </summary>
        /// <param name="chainTipToExtand">Tip of the chain to extend.</param>
        /// <param name="chainToConnect">The chain to connect.</param>
        /// <exception cref="ConsensusException">Thrown in case CHT is not in consistent state.</exception>
        private async Task<ConnectBlocksResult> ConnectBlocksAsync(ChainedHeader chainTipToExtand, List<ChainedHeaderBlock> chainToConnect)
        {
            ConnectBlocksResult result = null;
            ChainedHeader lastValidatedBlock = null;

            foreach (ChainedHeaderBlock nextChainedHeaderBlock in chainToConnect)
            {
                if ((nextChainedHeaderBlock.ChainedHeader.BlockValidationState != ValidationState.PartiallyValidated) &&
                    (nextChainedHeaderBlock.ChainedHeader.BlockValidationState != ValidationState.FullyValidated))
                {
                    this.logger.LogError("Block '{0}' must be partially or fully validated but it is {1}.", nextChainedHeaderBlock, nextChainedHeaderBlock.ChainedHeader.BlockValidationState);
                    this.logger.LogTrace("(-)[BLOCK_INVALID_STATE]");
                    throw new ConsensusException("Block must be partially or fully validated.");
                }

                var validationContext = new ValidationContext() { Block = nextChainedHeaderBlock.Block };

                // Call the validation engine.
                await this.consensusRules.AcceptBlockAsync(validationContext, chainTipToExtand).ConfigureAwait(false);

                if (validationContext.Error != null)
                {
                    List<int> badPeers;

                    lock (this.peerLock)
                    {
                        badPeers = this.chainedHeaderTree.PartialOrFullValidationFailed(nextChainedHeaderBlock.ChainedHeader);
                    }

                    result = new ConnectBlocksResult(false, false, badPeers, validationContext.Error.Message, validationContext.BanDurationSeconds);
                    break;
                }

                lock (this.peerLock)
                {
                    this.chainedHeaderTree.FullValidationSucceeded(nextChainedHeaderBlock.ChainedHeader);
                }

                chainTipToExtand = nextChainedHeaderBlock.ChainedHeader;
                lastValidatedBlock = chainTipToExtand;
            }

            if (result == null)
                result = new ConnectBlocksResult(true);

            result.LastValidatedBlockHeader = lastValidatedBlock;

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
                ChainedHeaderBlock chainedHeaderBlock = await this.LoadBlockDataAsync(currentHeader.HashBlock).ConfigureAwait(false);

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
            lock (this.peerLock)
            {
                return this.SetConsensusTipLocked(newTip);
            }
        }

        /// <summary>Sets the consensus tip.</summary>
        /// <remarks>Have to be locked by <see cref="peerLock"/>.</remarks>
        /// <param name="newTip">New consensus tip.</param>
        private List<int> SetConsensusTipLocked(ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            List<int> peerIdsToResync = this.chainedHeaderTree.ConsensusTipChanged(newTip);

            this.Tip = newTip;

            this.chainState.ConsensusTip = this.Tip;
            this.chain.SetTip(this.Tip);

            bool ibd = this.ibdState.IsInitialBlockDownload();

            if (ibd != this.isIbd)
                this.blockPuller.OnIbdStateChanged(ibd);

            this.isIbd = ibd;

            this.logger.LogTrace("(-):*.{0}={1}", nameof(peerIdsToResync.Count), peerIdsToResync.Count);
            return peerIdsToResync;
        }

        /// <summary>
        /// Request a list of block headers to download their respective blocks.
        /// If <paramref name="chainedHeaders"/> is not an array of consecutive headers it will be split to batches of consecutive header requests.
        /// Callbacks of all entries are added to <see cref="callbacksByBlocksRequestedHash"/>. If a block header was already requested
        /// to download and not delivered yet, it will not be requested again, instead just it's callback will be called when the block arrives.
        /// </summary>
        /// <param name="chainedHeaders">Array of chained headers to download.</param>
        /// <param name="onBlockDownloadedCallback">A callback to call when the block was downloaded.</param>
        private void DownloadBlocks(ChainedHeader[] chainedHeaders, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(chainedHeaders), nameof(chainedHeaders.Length), chainedHeaders.Length);

            var downloadRequests = new List<BlockDownloadRequest>();

            BlockDownloadRequest request = null;
            ChainedHeader previousHeader = null;

            lock (this.blockRequestedLock)
            {
                foreach (ChainedHeader chainedHeader in chainedHeaders)
                {
                    bool blockAlreadyAsked = this.callbacksByBlocksRequestedHash.TryGetValue(chainedHeader.HashBlock, out List<OnBlockDownloadedCallback> callbacks);

                    if (!blockAlreadyAsked)
                    {
                        callbacks = new List<OnBlockDownloadedCallback>();
                        this.callbacksByBlocksRequestedHash.Add(chainedHeader.HashBlock, callbacks);
                    }
                    else
                    {
                        this.logger.LogTrace("Registered additional callback for the block '{0}'.", chainedHeader);
                    }

                    callbacks.Add(onBlockDownloadedCallback);

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

            this.logger.LogTrace("(-)");
        }

        private void BlockDownloaded(uint256 blockHash, Block block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            ChainedHeader chainedHeader = null;

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

                if (block != null)
                {
                    try
                    {
                        chainedHeader = this.chainedHeaderTree.FindHeaderAndVerifyBlockIntegrity(block);
                    }
                    catch (BlockDownloadedForMissingChainedHeaderException)
                    {
                        this.logger.LogTrace("(-)[CHAINED_HEADER_NOT_FOUND]");
                        return;
                    }
                    //catch (BlockIntegrityVerificationException)
                    //{
                    //    // TODO: catch validation exceptions.
                    //    // TODO ban the peer, disconnect, return
                    //    // this.logger.LogTrace("(-)[INTEGRITY_VERIFICATION_FAILED]");
                    //    return;
                    //}
                }
                else
                {
                    this.logger.LogDebug("Block '{0}' failed to be delivered.", blockHash);
                }
            }

            List<OnBlockDownloadedCallback> listOfCallbacks = null;

            lock (this.blockRequestedLock)
            {
                if (this.callbacksByBlocksRequestedHash.TryGetValue(blockHash, out listOfCallbacks))
                    this.callbacksByBlocksRequestedHash.Remove(blockHash);
            }

            if (listOfCallbacks != null)
            {
                ChainedHeaderBlock chainedHeaderBlock = null;

                if (block != null)
                    chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);

                this.logger.LogTrace("Calling {0} callbacks for block '{1}'.", listOfCallbacks.Count, chainedHeader);
                foreach (OnBlockDownloadedCallback blockDownloadedCallback in listOfCallbacks)
                    blockDownloadedCallback(chainedHeaderBlock);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Provides block data for the given block hashes.
        /// </summary>
        /// <remarks>
        /// First we check if the block exists in chained header tree, then it check the block store and if it wasn't found there the block will be scheduled for download.
        /// Given callback is called when the block is obtained. If obtaining the block fails the callback will be called with <c>null</c>.
        /// </remarks>
        /// <param name="blockHashes">The block hashes to download.</param>
        /// <param name="onBlockDownloadedCallback">The callback that will be called for each downloaded block.</param>
        public async Task GetOrDownloadBlocksAsync(List<uint256> blockHashes, OnBlockDownloadedCallback onBlockDownloadedCallback)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashes), nameof(blockHashes.Count), blockHashes.Count);

            var blocksToDownload = new List<ChainedHeader>();

            for (int i = 0; i < blockHashes.Count; i++)
            {
                uint256 blockHash = blockHashes[i];

                ChainedHeaderBlock chainedHeaderBlock = await this.LoadBlockDataAsync(blockHash).ConfigureAwait(false);

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
                this.DownloadBlocks(blocksToDownload.ToArray(), this.ProcessDownloadedBlock);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Loads the block data from <see cref="chainedHeaderTree"/> or block store if it's enabled.</summary>
        /// <param name="blockHash">The block hash.</param>
        private async Task<ChainedHeaderBlock> LoadBlockDataAsync(uint256 blockHash)
        {
            this.logger.LogTrace("({0}:{1})", nameof(blockHash), blockHash);

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

            if (this.blockStore != null)
            {
                Block block = await this.blockStore.GetBlockAsync(blockHash).ConfigureAwait(false);
                if (block != null)
                {
                    var newBlockPair = new ChainedHeaderBlock(block, chainedHeaderBlock.ChainedHeader);
                    this.logger.LogTrace("Chained header block '{0}' was found in store.", newBlockPair);
                    this.logger.LogTrace("(-)[FOUND_IN_BLOCK_STORE]:'{0}'", newBlockPair);
                    return newBlockPair;
                }
            }

            this.logger.LogTrace("(-)[NOT_FOUND]:'{0}'", chainedHeaderBlock);
            return chainedHeaderBlock;
        }

        /// <summary>
        /// Processes items in the <see cref="toDownloadQueue"/> and ask the block puller for blocks to download.
        /// If the tree has too many unconsumed blocks we will not ask block puller for more until some blocks are consumed.
        /// </summary>
        /// <remarks>
        /// Requests that have too many blocks will be split in batches.
        /// The amount of blocks in 1 batch to downloaded depends on the average value in <see cref="IBlockPuller.AverageBlockSize"/>.
        /// </remarks>
        private void ProcessDownloadQueueLocked()
        {
            this.logger.LogTrace("()");

            while (this.toDownloadQueue.Count > 0)
            {
                BlockDownloadRequest request = this.toDownloadQueue.Peek();

                long freeBytes = MaxUnconsumedBlocksDataBytes - this.chainedHeaderTree.UnconsumedBlocksDataBytes - this.expectedBlockDataBytes;
                this.logger.LogTrace("{0} bytes worth of blocks is available for download.", freeBytes);

                if (freeBytes <= ConsumptionThresholdBytes)
                {
                    this.logger.LogTrace("(-)[THRESHOLD_NOT_MET]");
                    return;
                }

                long avgSize = (long)this.blockPuller.GetAverageBlockSizeBytes();
                int blocksToAsk = avgSize != 0 ? (int)(freeBytes / avgSize) : DefaultNumberOfBlocksToAsk;

                this.logger.LogTrace("With {0} average block size, we have {1} download slots available.", avgSize, blocksToAsk);

                if (request.BlocksToDownload.Count <= blocksToAsk)
                {
                    this.toDownloadQueue.Dequeue();
                }
                else
                {
                    this.logger.LogTrace("Splitting enqueued job of size {0} into 2 pieces of sizes {1} and {2}.", request.BlocksToDownload.Count, blocksToAsk, request.BlocksToDownload.Count - blocksToAsk);

                    // Split queue item in 2 pieces: one of size blocksToAsk and second is the rest. Ask BP for first part, leave 2nd part in the queue.
                    var blockPullerRequest = new BlockDownloadRequest()
                    {
                        BlocksToDownload = new List<ChainedHeader>(request.BlocksToDownload.GetRange(0, blocksToAsk))
                    };

                    request.BlocksToDownload.RemoveRange(0, blocksToAsk);

                    request = blockPullerRequest;
                }

                this.blockPuller.RequestBlocksDownload(request.BlocksToDownload);

                foreach (ChainedHeader chainedHeader in request.BlocksToDownload)
                    this.expectedBlockSizes.Add(chainedHeader.HashBlock, avgSize);

                this.expectedBlockDataBytes += request.BlocksToDownload.Count * avgSize;

                this.logger.LogTrace("Expected block data bytes was set to {0} and we are expecting {1} blocks to be delivered.", this.expectedBlockDataBytes, this.expectedBlockSizes.Count);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.reorgLock.Dispose();
        }

        /// <summary>
        /// Information related to the block full validation process.
        /// </summary>
        private class ConnectBlocksResult : ValidationResult
        {
            public bool ConsensusTipChanged { get; private set; }

            /// <summary>List of peer IDs to be banned and disconnected.</summary>
            /// <remarks><c>null</c> in case <see cref="ValidationResult.Succeeded"/> is <c>false</c>.</remarks>
            public List<int> PeersToBan { get; private set; }

            public ChainedHeader LastValidatedBlockHeader { get; set; }

            public ConnectBlocksResult(bool succeeded, bool consensusTipChanged = true, List<int> peersToBan = null, string banReason = null, int banDurationSeconds = 0)
            {
                this.ConsensusTipChanged = consensusTipChanged;
                this.Succeeded = succeeded;
                this.PeersToBan = peersToBan;
                this.BanReason = banReason;
                this.BanDurationSeconds = banDurationSeconds;
            }

            public override string ToString()
            {
                if (this.Succeeded)
                    return $"{nameof(this.Succeeded)}={this.Succeeded}";

                return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.ConsensusTipChanged)}={this.ConsensusTipChanged},{nameof(this.PeersToBan)}.{nameof(this.PeersToBan.Count)}={this.PeersToBan.Count},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
            }
        }
    }

    /// <summary>
    /// A delegate that is used to send callbacks when a bock is downloaded from the of queued requests to downloading blocks.
    /// </summary>
    /// <param name="chainedHeaderBlock">The pair of the block and its chained header.</param>
    public delegate void OnBlockDownloadedCallback(ChainedHeaderBlock chainedHeaderBlock);

    /// <summary>
    /// A request that holds information of blocks to download.
    /// </summary>
    public class BlockDownloadRequest
    {
        /// <summary>The list of block headers to download.</summary>
        public List<ChainedHeader> BlocksToDownload { get; set; }
    }

    public class PartialValidationResult : ValidationResult
    {
        public ChainedHeaderBlock ChainedHeaderBlock { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Succeeded)
                return base.ToString();

            return base.ToString() + $",{nameof(this.ChainedHeaderBlock)}={this.ChainedHeaderBlock}";
        }
    }

    public class ValidationResult
    {
        public bool Succeeded { get; set; }

        public int BanDurationSeconds { get; set; }

        public string BanReason { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Succeeded)
                return $"{nameof(this.Succeeded)}={this.Succeeded}";

            return $"{nameof(this.Succeeded)}={this.Succeeded},{nameof(this.BanReason)}={this.BanReason},{nameof(this.BanDurationSeconds)}={this.BanDurationSeconds}";
        }
    }
}