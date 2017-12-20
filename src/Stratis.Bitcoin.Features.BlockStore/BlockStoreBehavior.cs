using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreBehavior : INetworkPeerBehavior
    {
        bool CanRespondToGetDataPayload { get; set; }

        bool CanRespondToGetBlocksPayload { get; set; }

        /// <summary>
        /// Sends information about newly discovered blocks to network peers using "headers" or "inv" message.
        /// </summary>
        /// <param name="blocksToAnnounce">List of block headers to announce.</param>
        Task AnnounceBlocksAsync(List<ChainedBlock> blocksToAnnounce);
    }

    public class BlockStoreBehavior : NetworkPeerBehavior, IBlockStoreBehavior
    {
        // TODO: move this to the options
        // Maximum number of headers to announce when relaying blocks with headers message.
        private const int MAX_BLOCKS_TO_ANNOUNCE = 8;

        private readonly ConcurrentChain chain;

        private readonly IBlockRepository blockRepository;

        private readonly IBlockStoreCache blockStoreCache;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <inheritdoc />
        public bool CanRespondToGetBlocksPayload { get; set; }

        /// <inheritdoc />
        public bool CanRespondToGetDataPayload { get; set; }

        // local resources
        public bool PreferHeaders;// public for testing

        private bool preferHeaderAndIDs;

        public BlockStoreBehavior(
            ConcurrentChain chain,
            BlockRepository blockRepository,
            IBlockStoreCache blockStoreCache,
            ILoggerFactory loggerFactory)
            : this(chain, blockRepository as IBlockRepository, blockStoreCache, loggerFactory)
        {
        }

        public BlockStoreBehavior(ConcurrentChain chain, IBlockRepository blockRepository, IBlockStoreCache blockStoreCache, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(blockRepository, nameof(blockRepository));
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.chain = chain;
            this.blockRepository = blockRepository;
            this.blockStoreCache = blockStoreCache;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;

            this.CanRespondToGetBlocksPayload = false;
            this.CanRespondToGetDataPayload = true;

            this.PreferHeaders = false;
            this.preferHeaderAndIDs = false;
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived += this.AttachedNode_MessageReceivedAsync;

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived -= this.AttachedNode_MessageReceivedAsync;

            this.logger.LogTrace("(-)");
        }

        private async void AttachedNode_MessageReceivedAsync(NetworkPeer node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node?.RemoteSocketEndpoint, nameof(message), message?.Message?.Command);

            try
            {
                await this.ProcessMessageAsync(node, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException opx)
            {
                if (!opx.CancellationToken.IsCancellationRequested)
                    if (this.AttachedPeer?.IsConnected ?? false)
                    {
                        this.logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                        throw;
                    }

                // do nothing
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        private Task ProcessMessageAsync(NetworkPeer node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node?.RemoteSocketEndpoint, nameof(message), message?.Message?.Command);

            var getDataPayload = message.Message.Payload as GetDataPayload;
            if ((getDataPayload != null) && this.CanRespondToGetDataPayload)
            {
                Task res = this.ProcessGetDataAsync(node, getDataPayload);
                this.logger.LogTrace("(-)[GET_DATA_PAYLOAD]");
                return res;
            }

            // TODO: this is not used in core anymore consider deleting it
            ////var getBlocksPayload = message.Message.Payload as GetBlocksPayload;
            ////if (getBlocksPayload != null && this.CanRespondToGetBlocksPayload)
            ////   return this.ProcessGetBlocksAsync(node, getBlocksPayload);

            var sendCmpctPayload = message.Message.Payload as SendCmpctPayload;
            if (sendCmpctPayload != null)
            {
                Task res = this.ProcessSendCmpctPayload(node, sendCmpctPayload);
                this.logger.LogTrace("(-)[SEND_CMPCT_PAYLOAD]");
                return res;
            }

            var sendHeadersPayload = message.Message.Payload as SendHeadersPayload;
            if (sendHeadersPayload != null)
                this.PreferHeaders = true;

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
        }

        private Task ProcessSendCmpctPayload(NetworkPeer node, SendCmpctPayload sendCmpct)
        {
            // TODO: announce using compact blocks
            return Task.CompletedTask;
        }

        private async Task ProcessGetDataAsync(NetworkPeer node, GetDataPayload getDataPayload)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}.{4}:{5})", nameof(node), node?.RemoteSocketEndpoint, nameof(getDataPayload), nameof(getDataPayload.Inventory), nameof(getDataPayload.Inventory.Count), getDataPayload.Inventory.Count);
            Guard.Assert(node != null);

            // TODO: bring logic from core
            foreach (InventoryVector item in getDataPayload.Inventory.Where(inv => inv.Type.HasFlag(InventoryType.MSG_BLOCK)))
            {
                // TODO: check if we need to add support for "not found"
                Block block = await this.blockStoreCache.GetBlockAsync(item.Hash).ConfigureAwait(false);

                if (block != null)
                {
                    this.logger.LogTrace("Sending block '{0}' to peer '{1}'.", item.Hash, node?.RemoteSocketEndpoint);

                    //TODO strip block of witness if node does not support
                    await node.SendMessageAsync(new BlockPayload(block.WithOptions(node.SupportedTransactionOptions))).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        private async Task SendAsBlockInventoryAsync(NetworkPeer peer, IEnumerable<uint256> blocks)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.Count:{3})", nameof(peer), peer?.RemoteSocketEndpoint, nameof(blocks), blocks.Count());

            var queue = new Queue<InventoryVector>(blocks.Select(s => new InventoryVector(InventoryType.MSG_BLOCK, s)));
            while (queue.Count > 0)
            {
                var items = queue.TakeAndRemove(ConnectionManager.MaxInventorySize).ToArray();
                if (peer.IsConnected)
                {
                    this.logger.LogTrace("Sending inventory message to peer '{0}'.", peer.RemoteSocketEndpoint);
                    await peer.SendMessageAsync(new InvPayload(items)).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task AnnounceBlocksAsync(List<ChainedBlock> blocksToAnnounce)
        {
            Guard.NotNull(blocksToAnnounce, nameof(blocksToAnnounce));
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blocksToAnnounce), nameof(blocksToAnnounce.Count), blocksToAnnounce.Count);

            if (!blocksToAnnounce.Any())
            {
                this.logger.LogTrace("(-)[NO_BLOCKS]");
                return;
            }

            NetworkPeer peer = this.AttachedPeer;
            if (peer == null)
            {
                this.logger.LogTrace("(-)[NO_PEER]");
                return;
            }

            bool revertToInv = ((!this.PreferHeaders &&
                                 (!this.preferHeaderAndIDs || blocksToAnnounce.Count > 1)) ||
                                blocksToAnnounce.Count > MAX_BLOCKS_TO_ANNOUNCE);

            this.logger.LogTrace("Block propagation preferences of the peer '{0}': prefer headers - {1}, prefer headers and IDs - {2}, will{3} revert to 'inv' now.", peer.RemoteSocketEndpoint, this.PreferHeaders, this.preferHeaderAndIDs, revertToInv ? "" : " NOT");

            var headers = new List<BlockHeader>();
            var inventoryBlockToSend = new List<uint256>();

            var chainBehavior = peer.Behavior<ChainHeadersBehavior>();
            ChainedBlock bestIndex = null;
            if (!revertToInv)
            {
                bool foundStartingHeader = false;
                // Try to find first chained block that the peer doesn't have, and then add all chained blocks past that one.

                foreach (ChainedBlock chainedBlock in blocksToAnnounce)
                {
                    bestIndex = chainedBlock;

                    if (!foundStartingHeader)
                    {
                        this.logger.LogTrace("Checking is the peer '{0}' can connect header '{1}'.", peer.RemoteSocketEndpoint, chainedBlock);

                        // Peer doesn't have a block at the height of our block and with the same hash?
                        if (chainBehavior.PendingTip.FindAncestorOrSelf(chainedBlock) != null)
                        {
                            this.logger.LogTrace("Peer '{0}' does not have header '{1}'.", peer.RemoteSocketEndpoint, chainedBlock.Previous);
                            continue;
                        }

                        // Peer doesn't have a block at the height of our block.Previous and with the same hash?
                        if (chainBehavior.PendingTip.FindAncestorOrSelf(chainedBlock.Previous) == null)
                        {
                            // Peer doesn't have this header or the prior one - nothing will connect, so bail out.
                            this.logger.LogTrace("Neither the header nor its previous header found for peer '{0}', reverting to 'inv'.", peer.RemoteSocketEndpoint);
                            revertToInv = true;
                            break;
                        }

                        this.logger.LogTrace("Peer '{0}' can connect header '{1}'.", peer.RemoteSocketEndpoint, chainedBlock.Previous);
                        foundStartingHeader = true;
                    }

                    // If we reached here then it means that we've found starting header.
                    headers.Add(chainedBlock.Header);
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

                    chainBehavior.SetPendingTip(bestIndex);
                    await peer.SendMessageAsync(new HeadersPayload(headers.ToArray())).ConfigureAwait(false);
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
                    ChainedBlock chainedBlock = blocksToAnnounce.Last();
                    if (chainedBlock != null)
                    {
                        if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Height) == null)
                        {
                            inventoryBlockToSend.Add(chainedBlock.HashBlock);
                            this.logger.LogDebug("Sending inventory hash '{0}' to peer '{1}'.", chainedBlock.HashBlock, peer.RemoteSocketEndpoint);
                        }
                    }
                }
            }

            if (inventoryBlockToSend.Any())
            {
                await this.SendAsBlockInventoryAsync(peer, inventoryBlockToSend).ConfigureAwait(false);
                this.logger.LogTrace("(-)[SEND_INVENTORY]");
                return;
            }

            this.logger.LogTrace("(-)");
        }

        public override object Clone()
        {
            this.logger.LogTrace("()");

            var res = new BlockStoreBehavior(this.chain, this.blockRepository, this.blockStoreCache, this.loggerFactory)
            {
                CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
                CanRespondToGetDataPayload = this.CanRespondToGetDataPayload
            };

            this.logger.LogTrace("(-)");
            return res;
        }
    }
}
