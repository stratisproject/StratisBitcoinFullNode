using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreBehavior : INodeBehavior
    {
        bool CanRespondeToGetDataPayload { get; set; }

        bool CanRespondToGetBlocksPayload { get; set; }

        Task AnnounceBlocks(List<uint256> blockHashesToAnnounce);
    }

    public class BlockStoreBehavior : NodeBehavior
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

        public bool CanRespondToGetBlocksPayload { get; set; }

        public bool CanRespondToGetDataPayload { get; set; }

        // local resources
        public bool PreferHeaders; // public for testing
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

            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceivedAsync;

            this.logger.LogTrace("(-)");
        }

        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceivedAsync;

            this.logger.LogTrace("(-)");
        }

        private async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node?.RemoteSocketEndpoint, nameof(message), message?.Message?.Command);

            try
            {
                await this.ProcessMessageAsync(node, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException opx)
            {
                if (!opx.CancellationToken.IsCancellationRequested)
                    if (this.AttachedNode?.IsConnected ?? false)
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

        private Task ProcessMessageAsync(Node node, IncomingMessage message)
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

        private Task ProcessSendCmpctPayload(Node node, SendCmpctPayload sendCmpct)
        {
            // TODO: announce using compact blocks
            return Task.CompletedTask;
        }

        private async Task ProcessGetDataAsync(Node node, GetDataPayload getDataPayload)
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

        private async Task SendAsBlockInventoryAsync(Node node, IEnumerable<uint256> blocks)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.Count:{3})", nameof(node), node?.RemoteSocketEndpoint, nameof(blocks), blocks.Count());

            var queue = new Queue<InventoryVector>(blocks.Select(s => new InventoryVector(InventoryType.MSG_BLOCK, s)));
            while (queue.Count > 0)
            {
                var items = queue.TakeAndRemove(ConnectionManager.MaxInventorySize).ToArray();
                if (node.IsConnected)
                {
                    this.logger.LogTrace("Sending inventory message to peer '{0}'.", node.RemoteSocketEndpoint);
                    await node.SendMessageAsync(new InvPayload(items));
                }
            }

            this.logger.LogTrace("(-)");
        }

        public Task AnnounceBlocks(List<uint256> blockHashesToAnnounce)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blockHashesToAnnounce), nameof(blockHashesToAnnounce.Count), blockHashesToAnnounce?.Count);
            Guard.NotNull(blockHashesToAnnounce, nameof(blockHashesToAnnounce));

            if (!blockHashesToAnnounce.Any())
            {
                this.logger.LogTrace("(-)[NO_HASHES]");
                return Task.CompletedTask;
            }

            Node node = this.AttachedNode;
            if (node == null)
            {
                this.logger.LogTrace("(-)[NO_NODE]");
                return Task.CompletedTask;
            }

            bool revertToInv = ((!this.PreferHeaders &&
                                 (!this.preferHeaderAndIDs || blockHashesToAnnounce.Count > 1)) ||
                                blockHashesToAnnounce.Count > MAX_BLOCKS_TO_ANNOUNCE);

            var headers = new List<BlockHeader>();
            var inventoryBlockToSend = new List<uint256>();

            var chainBehavior = node.Behavior<ChainHeadersBehavior>();
            ChainedBlock bestIndex = null;
            if (!revertToInv)
            {
                bool foundStartingHeader = false;
                // Try to find first header that our peer doesn't have, and
                // then send all headers past that one.  If we come across any
                // headers that aren't on chainActive, give up.

                foreach (var hash in blockHashesToAnnounce)
                {
                    ChainedBlock chainedBlock = this.chain.GetBlock(hash);
                    if (chainedBlock == null)
                    {
                        // Bail out if we reorged away from this block
                        revertToInv = true;
                        break;
                    }

                    bestIndex = chainedBlock;
                    if (foundStartingHeader)
                    {
                        headers.Add(chainedBlock.Header);
                    }
                    else if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Height) != null)
                    {
                        continue;
                    }
                    else if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Previous.Height) != null)
                    {
                        // Peer doesn't have this header but they do have the prior one.
                        // Start sending headers.
                        foundStartingHeader = true;
                        headers.Add(chainedBlock.Header);
                    }
                    else
                    {
                        // Peer doesn't have this header or the prior one -- nothing will
                        // connect, so bail out.
                        revertToInv = true;
                        break;
                    }
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
                    if (headers.Count > 1) this.logger.LogDebug("Sending {0} headers, range {1} - {2}, to peer '{3}'.", headers.Count, headers.First(), headers.Last(), node.RemoteSocketEndpoint);
                    else this.logger.LogDebug("Sending header {0} to peer '{1}'.", headers.First(), node.RemoteSocketEndpoint);

                    chainBehavior.SetPendingTip(bestIndex);
                    Task res = node.SendMessageAsync(new HeadersPayload(headers.ToArray()));
                    this.logger.LogTrace("(-)[SEND_HEADERS_PAYLOAD]");
                    return res;
                }
                else
                {
                    revertToInv = true;
                }
            }

            if (revertToInv)
            {
                // If falling back to using an inv, just try to inv the tip.
                // The last entry in vBlockHashesToAnnounce was our tip at some point
                // in the past.

                if (blockHashesToAnnounce.Any())
                {
                    uint256 hashToAnnounce = blockHashesToAnnounce.Last();
                    ChainedBlock chainedBlock = this.chain.GetBlock(hashToAnnounce);
                    if (chainedBlock != null)
                    {
                        if (chainBehavior.PendingTip.GetAncestor(chainedBlock.Height) == null)
                        {
                            inventoryBlockToSend.Add(hashToAnnounce);
                            this.logger.LogDebug("Sending inventory hash '{0}' to peer '{1}'.", hashToAnnounce, node.RemoteSocketEndpoint);
                        }
                    }
                }
            }

            if (inventoryBlockToSend.Any())
            {
                Task res = this.SendAsBlockInventoryAsync(node, inventoryBlockToSend);
                this.logger.LogTrace("(-)[SEND_INVENTORY]");
                return res;
            }

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
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
