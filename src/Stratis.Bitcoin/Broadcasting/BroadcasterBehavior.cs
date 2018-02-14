using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Broadcasting
{
    public class BroadcasterBehavior : NetworkPeerBehavior
    {
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Instance logger for the memory pool component.</summary>
        private readonly ILogger logger;

        public BroadcasterBehavior(
            IBroadcasterManager broadcasterManager,
            ILogger logger)
        {
            this.logger = logger;
            this.broadcasterManager = broadcasterManager;

            this.SubscribeToPayload<InvPayload>(this.ProcessInvPayloadAsync);
            this.SubscribeToPayload<GetDataPayload>(this.ProcessGetDataPayloadAsync);
        }

        public BroadcasterBehavior(
            IBroadcasterManager broadcasterManager,
            ILoggerFactory loggerFactory)
            : this(broadcasterManager, loggerFactory.CreateLogger(typeof(BroadcasterBehavior).FullName))
        {
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BroadcasterBehavior(this.broadcasterManager, this.logger);
        }

        ///TODO: Fix the exception handling of the async event.
        private async Task ProcessInvPayloadAsync(InvPayload invPayload, INetworkPeer peer)
        {
            try
            {
                // if node has tx we broadcasted
                foreach (var inv in invPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
                {
                    var txEntry = this.broadcasterManager.GetTransaction(inv.Hash);
                    if (txEntry != null)
                    {
                        this.broadcasterManager.AddOrUpdate(txEntry.Transaction, State.Propagated);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());
                throw;
            }
        }

        ///TODO: Fix the exception handling of the async event.
        private async Task ProcessGetDataPayloadAsync(GetDataPayload getDataPayload, INetworkPeer peer)
        {
            try
            {
                // If node asks for tx we want to broadcast.
                foreach (InventoryVector inv in getDataPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
                {
                    TransactionBroadcastEntry txEntry = this.broadcasterManager.GetTransaction(inv.Hash);
                    if ((txEntry != null) && (txEntry.State != State.CantBroadcast))
                    {
                        await peer.SendMessageAsync(new TxPayload(txEntry.Transaction)).ConfigureAwait(false);
                        if (txEntry.State == State.ToBroadcast)
                        {
                            this.broadcasterManager.AddOrUpdate(txEntry.Transaction, State.Broadcasted);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());
                throw;
            }
        }
    }
}
