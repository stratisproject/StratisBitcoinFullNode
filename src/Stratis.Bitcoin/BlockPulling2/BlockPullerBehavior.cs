using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    public class BlockPullerBehavior : NetworkPeerBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly BlockPuller blockPuller;

        public BlockPullerBehavior(BlockPuller blockPuller, ILoggerFactory loggerFactory)
        {
            this.blockPuller = blockPuller;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (message.Message.Payload is BlockPayload block)
            {
                this.blockPuller.PushBlock(block.Obj.GetHash(), block.Obj, this.AttachedPeer.Connection.Id);
            }

            this.logger.LogTrace("(-)");
        }

        public async Task RequestBlocksAsync(List<uint256> hashes)
        {
            var getDataPayload = new GetDataPayload();

            foreach (uint256 uint256 in hashes)
            {
                var vector = new InventoryVector(InventoryType.MSG_BLOCK, uint256);
                vector.Type = this.AttachedPeer.AddSupportedOptions(vector.Type);

                getDataPayload.Inventory.Add(vector);
            }

            if ((this.AttachedPeer == null) || (this.AttachedPeer.State != NetworkPeerState.HandShaked))
            {
                this.logger.LogTrace("(-)[ATTACHED_PEER]");
                throw new Exception("Peer is in the wrong state!");
            }

            await this.AttachedPeer.SendMessageAsync(getDataPayload).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BlockPullerBehavior(this.blockPuller, this.loggerFactory);
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }
        
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");
            
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            
            this.logger.LogTrace("(-)");
        }
    }
}
