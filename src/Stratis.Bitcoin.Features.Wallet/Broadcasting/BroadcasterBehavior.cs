using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
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
        }

        public BroadcasterBehavior(
            IBroadcasterManager broadcasterManager,
            ILoggerFactory loggerFactory)
            : this(broadcasterManager, loggerFactory.CreateLogger(typeof(BroadcasterBehavior).FullName))
        {
        }

        /// <inheritdoc />
        [NoTrace]
        public override object Clone()
        {
            return new BroadcasterBehavior(this.broadcasterManager, this.logger);
        }

        /// <summary>
        /// Handler for processing incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="message">Incoming message.</param>
        /// <remarks>
        /// TODO: Fix the exception handling of the async event.
        /// </remarks>
        protected async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(peer, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());

                // while in dev catch any unhandled exceptions
                Debugger.Break();
                throw;
            }
        }

        /// <summary>
        /// Handler for processing peer messages.
        /// Handles the following message payloads: TxPayload, MempoolPayload, GetDataPayload, InvPayload.
        /// </summary>
        /// <param name="peer">Peer sending the message.</param>
        /// <param name="message">Incoming message.</param>
        protected async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetDataPayload getDataPayload:
                    await this.ProcessGetDataPayloadAsync(peer, getDataPayload).ConfigureAwait(false);
                    break;

                case InvPayload invPayload:
                    this.ProcessInvPayload(invPayload);
                    break;
            }
        }

        private void ProcessInvPayload(InvPayload invPayload)
        {
            // if node has transaction we broadcast
            foreach (InventoryVector inv in invPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                TransactionBroadcastEntry txEntry = this.broadcasterManager.GetTransaction(inv.Hash);
                if (txEntry != null)
                {
                    this.broadcasterManager.AddOrUpdate(txEntry.Transaction, State.Propagated);
                }
            }
        }

        protected async Task ProcessGetDataPayloadAsync(INetworkPeer peer, GetDataPayload getDataPayload)
        {
            // If node asks for transaction we want to broadcast.
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

        /// <inheritdoc />
        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }
    }
}
