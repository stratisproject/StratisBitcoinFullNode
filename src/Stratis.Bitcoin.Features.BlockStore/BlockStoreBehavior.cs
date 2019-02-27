using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreBehavior : INetworkPeerBehavior
    {
        bool CanRespondToGetDataPayload { get; set; }

        bool CanRespondToGetBlocksPayload { get; set; }

        /// <summary>
        /// Sends information about newly discovered blocks to network peers using "headers" or "inv" message.
        /// </summary>
        /// <param name="blocksToAnnounce">List of chained block headers to announce.</param>
        Task AnnounceBlocksAsync(List<ChainedHeader> blocksToAnnounce);
    }

    public class BlockStoreBehavior : NetworkPeerBehavior, IBlockStoreBehavior
    {
        // TODO: move this to the options
        // Maximum number of headers to announce when relaying blocks with headers message.
        private const int MaxBlocksToAnnounce = 8;

        protected readonly ConcurrentChain chain;

        protected readonly IConsensusManager consensusManager;
        protected readonly IBlockStoreQueue blockStoreQueue;

        protected ConsensusManagerBehavior consensusManagerBehavior;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <inheritdoc />
        public bool CanRespondToGetBlocksPayload { get; set; }

        /// <inheritdoc />
        public bool CanRespondToGetDataPayload { get; set; }

        /// <summary>Local resources.</summary>
        /// <remarks>Public for testing.</remarks>
        public bool PreferHeaders;

        private readonly bool preferHeaderAndIDs;

        /// <summary>Hash of the last block we've sent to the peer in response to "getblocks" message,
        /// or <c>null</c> if the peer haven't used "getblocks" message or if we sent a tip to it already.</summary>
        /// <remarks>
        /// In case the peer is syncing using outdated "getblocks" message, we need to maintain
        /// the hash of the last block we sent to it in an inventory batch. Once the peer asks
        /// for block data of the block with this hash, we will send a continuation inventory message.
        /// This will cause the peer to ask for more.
        /// </remarks>
        private uint256 getBlocksBatchLastItemHash;

        /// <summary>Chained header of the last header sent to the peer.</summary>
        private ChainedHeader lastSentHeader;

        protected readonly IChainState chainState;

        public BlockStoreBehavior(ConcurrentChain chain, IChainState chainState, ILoggerFactory loggerFactory, IConsensusManager consensusManager, IBlockStoreQueue blockStoreQueue)
        {
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(consensusManager, nameof(consensusManager));
            Guard.NotNull(blockStoreQueue, nameof(blockStoreQueue));

            this.chain = chain;
            this.chainState = chainState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.consensusManager = consensusManager;
            this.blockStoreQueue = blockStoreQueue;

            this.CanRespondToGetBlocksPayload = true;
            this.CanRespondToGetDataPayload = true;

            this.PreferHeaders = false;
            this.preferHeaderAndIDs = false;
        }

        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
            this.consensusManagerBehavior = this.AttachedPeer.Behavior<ConsensusManagerBehavior>();
        }

        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        [NoTrace]
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(peer, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                return;
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw;
            }
        }

        [NoTrace]
        protected virtual async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetDataPayload getDataPayload:
                    if (!this.CanRespondToGetDataPayload)
                    {
                        this.logger.LogTrace("Can't respond to 'getdata'.");
                        break;
                    }

                    await this.ProcessGetDataAsync(peer, getDataPayload).ConfigureAwait(false);
                    break;

                case GetBlocksPayload getBlocksPayload:
                    // TODO: this is not used in core anymore consider deleting it
                    // However, this is required for StratisX to be able to sync from us.

                    if (!this.CanRespondToGetBlocksPayload)
                    {
                        this.logger.LogTrace("Can't respond to 'getblocks'.");
                        break;
                    }

                    await this.ProcessGetBlocksAsync(peer, getBlocksPayload).ConfigureAwait(false);
                    break;

                case SendHeadersPayload sendHeadersPayload:
                    this.PreferHeaders = true;
                    break;
            }
        }

        /// <summary>
        /// Processes "getblocks" message received from the peer.
        /// </summary>
        /// <param name="peer">Peer that sent the message.</param>
        /// <param name="getBlocksPayload">Payload of "getblocks" message to process.</param>
        private async Task ProcessGetBlocksAsync(INetworkPeer peer, GetBlocksPayload getBlocksPayload)
        {
            if (getBlocksPayload.BlockLocators.Blocks.Count > BlockLocator.MaxLocatorSize)
            {
                this.logger.LogTrace("Peer '{0}' sent getblocks with oversized locator, disconnecting.", peer.RemoteSocketEndpoint);

                peer.Disconnect("Peer sent getblocks with oversized locator");

                this.logger.LogTrace("(-)[LOCATOR_TOO_LARGE]");
                return;
            }

            // We only want to work with blocks that are in the store,
            // so we first get information about the store's tip.
            ChainedHeader blockStoreTip = this.blockStoreQueue.BlockStoreCacheTip;
            if (blockStoreTip == null)
            {
                this.logger.LogTrace("(-)[REORG]");
                return;
            }

            // Now we want to find the last common block between our chain and the block locator the peer sent us.
            ChainedHeader chainTip = this.chain.Tip;
            ChainedHeader forkPoint = null;

            // Find last common block between our chain and the block locator the peer sent us.
            while (forkPoint == null)
            {
                forkPoint = this.chain.FindFork(getBlocksPayload.BlockLocators.Blocks);
                if (forkPoint == null)
                {
                    this.logger.LogTrace("(-)[NO_FORK_POINT]");
                    return;
                }

                // In case of reorg, we just try again, eventually we succeed.
                if (chainTip.FindAncestorOrSelf(forkPoint) == null)
                {
                    chainTip = this.chain.Tip;
                    forkPoint = null;
                }
            }

            this.logger.LogDebug("Block store tip is '{0}' and Fork point is '{1}'.", blockStoreTip, forkPoint);

            // If block store is lower than the fork point, or it is on different chain, we don't have anything to contribute to this peer at this point.
            if (blockStoreTip.FindAncestorOrSelf(forkPoint) == null)
            {
                this.logger.LogDebug("Fork point of peer '{0}' was not found on the block store chain.", peer.RemoteSocketEndpoint);
                this.logger.LogTrace("(-)[FORK_OUTSIDE_STORE]");
                return;
            }

            // Now we compile a list of blocks we want to send to the peer as inventory vectors.
            // This is needed as we want to traverse the chain in forward direction.
            int maxHeight = Math.Min(blockStoreTip.Height, forkPoint.Height + InvPayload.MaxGetBlocksInventorySize);
            ChainedHeader lastBlock = blockStoreTip.GetAncestor(maxHeight);
            int headersCount = maxHeight - forkPoint.Height;

            this.logger.LogTrace("Last block to announce is '{0}', number of blocks to announce is {1}.", lastBlock, headersCount);

            var headersToAnnounce = new ChainedHeader[headersCount];
            for (int i = headersCount - 1; i >= 0; i--)
            {
                headersToAnnounce[i] = lastBlock;
                lastBlock = lastBlock.Previous;
            }

            // Now we compile inventory payload and we also consider hash stop given by the peer.
            bool sendContinuation = true;
            ChainedHeader lastAddedChainedHeader = null;
            var inv = new InvPayload();
            for (int i = 0; i < headersToAnnounce.Length; i++)
            {
                ChainedHeader chainedHeader = headersToAnnounce[i];
                if (chainedHeader.HashBlock == getBlocksPayload.HashStop)
                {
                    this.logger.LogTrace("Hash stop has been reached.");
                    break;
                }

                this.logger.LogTrace("Adding block '{0}' to the inventory.", chainedHeader);
                lastAddedChainedHeader = chainedHeader;
                inv.Inventory.Add(new InventoryVector(InventoryType.MSG_BLOCK, chainedHeader.HashBlock));
                if (chainedHeader.HashBlock == chainTip.HashBlock)
                {
                    this.logger.LogDebug("Tip of the chain for peer '{0}' has been reached.", peer.RemoteSocketEndpoint);
                    sendContinuation = false;
                }
            }

            int count = inv.Inventory.Count;
            if (count > 0)
            {
                // If we reached the limmit size of inv, we need to tell the downloader to send another 'getblocks' message.
                if (count == InvPayload.MaxGetBlocksInventorySize && lastAddedChainedHeader != null)
                {
                    this.logger.LogDebug("Setting peer's last block sent to '{0}'.", lastAddedChainedHeader);
                    this.lastSentHeader = lastAddedChainedHeader;
                    this.consensusManagerBehavior.UpdateBestSentHeader(this.lastSentHeader);

                    // Set last item of the batch (unless we are announcing the tip), which is then used
                    // when the peer sends us "getdata" message. When we detect "getdata" message for this block,
                    // we will send continuation inventory message. This will cause the peer to ask for another batch of blocks.
                    // See ProcessGetDataAsync method.
                    if (sendContinuation)
                        this.getBlocksBatchLastItemHash = lastAddedChainedHeader.HashBlock;
                }

                this.logger.LogDebug("Sending inventory with {0} block hashes.", count);
                await peer.SendMessageAsync(inv).ConfigureAwait(false);
            }
            else this.logger.LogTrace("Nothing to send.");
        }

        private async Task ProcessGetDataAsync(INetworkPeer peer, GetDataPayload getDataPayload)
        {
            // TODO: bring logic from core
            foreach (InventoryVector item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_BLOCK)))
            {
                ChainedHeaderBlock chainedHeaderBlock = await this.consensusManager.GetBlockDataAsync(item.Hash).ConfigureAwait(false);

                if (chainedHeaderBlock?.Block != null)
                {
                    this.logger.LogTrace("Sending block '{0}' to peer '{1}'.", chainedHeaderBlock.ChainedHeader, peer.RemoteSocketEndpoint);

                    //TODO strip block of witness if node does not support
                    await peer.SendMessageAsync(new BlockPayload(chainedHeaderBlock.Block.WithOptions(this.chain.Network.Consensus.ConsensusFactory, peer.SupportedTransactionOptions))).ConfigureAwait(false);
                }
                else
                {
                    this.logger.LogTrace("Block with hash '{0}' requested from peer '{1}' was not found in store.", item.Hash, peer.RemoteSocketEndpoint);
                }

                // If the peer is syncing using "getblocks" message we are supposed to send
                // an "inv" message with our tip to it once it asks for all blocks
                // from the previous batch.
                if (item.Hash == this.getBlocksBatchLastItemHash)
                {
                    // Reset the hash to indicate that no continuation is pending anymore.
                    this.getBlocksBatchLastItemHash = null;

                    // Announce last block we have in the store.
                    ChainedHeader blockStoreTip = this.blockStoreQueue.BlockStoreCacheTip;
                    if (blockStoreTip != null)
                    {
                        this.logger.LogDebug("Sending continuation inventory message for block '{0}' to peer '{1}'.", blockStoreTip, peer.RemoteSocketEndpoint);
                        var invContinue = new InvPayload();
                        invContinue.Inventory.Add(new InventoryVector(InventoryType.MSG_BLOCK, blockStoreTip.HashBlock));
                        await peer.SendMessageAsync(invContinue).ConfigureAwait(false);
                    }
                    else
                    {
                        this.logger.LogDebug("Reorg in blockstore, inventory continuation won't be sent to peer '{0}'.", peer.RemoteSocketEndpoint);
                    }
                }
            }
        }

        private async Task SendAsBlockInventoryAsync(INetworkPeer peer, List<ChainedHeader> blocks)
        {
            // TODO please don't use queue here. Refactor it.
            var queue = new Queue<InventoryVector>(blocks.Select(s => new InventoryVector(InventoryType.MSG_BLOCK, s.HashBlock)));
            while (queue.Count > 0)
            {
                InventoryVector[] items = queue.TakeAndRemove(ConnectionManager.MaxInventorySize).ToArray();
                if (peer.IsConnected)
                {
                    this.logger.LogTrace("Sending inventory message to peer '{0}'.", peer.RemoteSocketEndpoint);
                    await peer.SendMessageAsync(new InvPayload(items)).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task AnnounceBlocksAsync(List<ChainedHeader> blocksToAnnounce)
        {
            Guard.NotNull(blocksToAnnounce, nameof(blocksToAnnounce));

            if (!blocksToAnnounce.Any())
            {
                this.logger.LogTrace("(-)[NO_BLOCKS]");
                return;
            }

            INetworkPeer peer = this.AttachedPeer;
            if (peer == null)
            {
                this.logger.LogTrace("(-)[NO_PEER]");
                return;
            }

            bool revertToInv = ((!this.PreferHeaders && (!this.preferHeaderAndIDs || blocksToAnnounce.Count > 1)) || blocksToAnnounce.Count > MaxBlocksToAnnounce);

            this.logger.LogTrace("Block propagation preferences of the peer '{0}': prefer headers - {1}, prefer headers and IDs - {2}, will{3} revert to 'inv' now.", peer.RemoteSocketEndpoint, this.PreferHeaders, this.preferHeaderAndIDs, revertToInv ? "" : " NOT");

            var headers = new List<BlockHeader>();
            var inventoryBlockToSend = new List<ChainedHeader>();

            try
            {
                ChainedHeader bestSentHeader = this.consensusManagerBehavior.BestSentHeader;

                ChainedHeader bestIndex = null;
                if (!revertToInv)
                {
                    bool foundStartingHeader = false;

                    // In case we don't have any information about peer's tip send him only last header and don't update best sent header.
                    // We expect peer to answer with getheaders message.
                    if (bestSentHeader == null)
                    {
                        await peer.SendMessageAsync(this.BuildHeadersAnnouncePayload(new[] { blocksToAnnounce.Last().Header })).ConfigureAwait(false);

                        this.logger.LogTrace("(-)[SENT_SINGLE_HEADER]");
                        return;
                    }

                    // Try to find first chained block that the peer doesn't have, and then add all chained blocks past that one.
                    foreach (ChainedHeader chainedHeader in blocksToAnnounce)
                    {
                        bestIndex = chainedHeader;

                        if (!foundStartingHeader)
                        {
                            this.logger.LogTrace("Checking is the peer '{0}' can connect header '{1}'.", peer.RemoteSocketEndpoint, chainedHeader);

                            // Peer doesn't have a block at the height of our block and with the same hash?
                            if (bestSentHeader?.FindAncestorOrSelf(chainedHeader) != null)
                            {
                                this.logger.LogTrace("Peer '{0}' already has header '{1}'.", peer.RemoteSocketEndpoint, chainedHeader.Previous);
                                continue;
                            }

                            // Peer doesn't have a block at the height of our block.Previous and with the same hash?
                            if (bestSentHeader?.FindAncestorOrSelf(chainedHeader.Previous) == null)
                            {
                                // Peer doesn't have this header or the prior one - nothing will connect, so bail out.
                                this.logger.LogTrace("Neither the header nor its previous header found for peer '{0}', reverting to 'inv'.", peer.RemoteSocketEndpoint);
                                revertToInv = true;
                                break;
                            }

                            this.logger.LogTrace("Peer '{0}' can connect header '{1}'.", peer.RemoteSocketEndpoint, chainedHeader.Previous);
                            foundStartingHeader = true;
                        }

                        // If we reached here then it means that we've found starting header.
                        headers.Add(chainedHeader.Header);
                    }
                }

                if (!revertToInv && headers.Any())
                {
                    if ((headers.Count == 1) && this.preferHeaderAndIDs)
                    {
                        // TODO:
                    }
                    else if (this.PreferHeaders)
                    {
                        if (headers.Count > 1) this.logger.LogDebug("Sending {0} headers, range {1} - {2}, to peer '{3}'.", headers.Count, headers.First(), headers.Last(), peer.RemoteSocketEndpoint);
                        else this.logger.LogDebug("Sending header '{0}' to peer '{1}'.", headers.First(), peer.RemoteSocketEndpoint);

                        this.lastSentHeader = bestIndex;
                        this.consensusManagerBehavior.UpdateBestSentHeader(this.lastSentHeader);

                        await peer.SendMessageAsync(this.BuildHeadersAnnouncePayload(headers)).ConfigureAwait(false);
                        this.logger.LogTrace("(-)[SEND_HEADERS_PAYLOAD]");
                        return;
                    }
                    else
                    {
                        revertToInv = true;
                    }
                }

                if (revertToInv)
                {
                    // If falling back to using an inv, just try to inv the tip.
                    // The last entry in 'blocksToAnnounce' was our tip at some point in the past.
                    if (blocksToAnnounce.Any())
                    {
                        ChainedHeader chainedHeader = blocksToAnnounce.Last();
                        if (chainedHeader != null)
                        {
                            if ((bestSentHeader == null) || (bestSentHeader.GetAncestor(chainedHeader.Height) == null))
                            {
                                inventoryBlockToSend.Add(chainedHeader);
                                this.logger.LogDebug("Sending inventory hash '{0}' to peer '{1}'.", chainedHeader.HashBlock, peer.RemoteSocketEndpoint);
                            }
                        }
                    }
                }

                if (inventoryBlockToSend.Any())
                {
                    this.lastSentHeader = inventoryBlockToSend.Last();
                    this.consensusManagerBehavior.UpdateBestSentHeader(this.lastSentHeader);

                    await this.SendAsBlockInventoryAsync(peer, inventoryBlockToSend).ConfigureAwait(false);
                    this.logger.LogTrace("(-)[SEND_INVENTORY]");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                return;
            }
        }

        /// <summary>
        /// Builds payload that announces to the peers new blocks that we've connected.
        /// This method can be overridden to return different type of HeadersPayload, e.g. <see cref="ProvenHeadersPayload" />
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>
        /// The <see cref="HeadersPayload" /> instance to announce to the peer.
        /// </returns>
        protected virtual Payload BuildHeadersAnnouncePayload(IEnumerable<BlockHeader> headers)
        {
            return new HeadersPayload(headers);
        }

        [NoTrace]
        public override object Clone()
        {
            var res = new BlockStoreBehavior(this.chain, this.chainState, this.loggerFactory, this.consensusManager, this.blockStoreQueue)
            {
                CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
                CanRespondToGetDataPayload = this.CanRespondToGetDataPayload
            };

            return res;
        }
    }
}
