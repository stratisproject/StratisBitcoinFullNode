﻿using System;
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
        protected readonly IBroadcastManager broadcastManager;

        /// <summary>Instance logger for the memory pool component.</summary>
        protected readonly ILogger logger;

        public BroadcasterBehavior(
            IBroadcastManager broadcastManager,
            ILogger logger)
        {
            this.logger = logger;
            this.broadcastManager = broadcastManager;
        }

        public BroadcasterBehavior(
            IBroadcastManager broadcastManager,
            ILoggerFactory loggerFactory)
            : this(broadcastManager, loggerFactory.CreateLogger(typeof(BroadcasterBehavior).FullName))
        {
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BroadcasterBehavior(this.broadcastManager, this.logger);
        }

        /// <summary>
        /// Handler for processing incoming message from node.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        /// <remarks>
        /// TODO: Fix the exception handling of the async event.
        /// </remarks>
        protected async void AttachedNode_MessageReceivedAsync(NetworkPeer node, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(node, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException opx)
            {
                if (!opx.CancellationToken.IsCancellationRequested)
                    if (this.AttachedPeer?.IsConnected ?? false)
                        throw;

                // do nothing
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
        /// Handler for processing node messages.
        /// Handles the following message payloads: TxPayload, MempoolPayload, GetDataPayload, InventoryPayload.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        protected Task ProcessMessageAsync(NetworkPeer node, IncomingMessage message)
        {
            if (message.Message.Payload is GetDataPayload getDataPayload)
            {
                this.ProcessGetDataPayload(node, getDataPayload);
                return Task.CompletedTask;
            }

            if (message.Message.Payload is InventoryPayload invPayload)
            {
                this.ProcessInvPayload(invPayload);
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        private void ProcessInvPayload(InventoryPayload inventoryPayload)
        {
            // if node has tx we broadcasted
            foreach (var inv in inventoryPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                var txEntry = this.broadcastManager.GetTransaction(inv.Hash);
                if (txEntry != null)
                {
                    this.broadcastManager.AddOrUpdate(txEntry.Transaction, State.Propagated);
                }
            }
        }

        protected void ProcessGetDataPayload(NetworkPeer node, GetDataPayload getDataPayload)
        {
            // if node asks for tx we want to broadcast
            foreach (var inv in getDataPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                var txEntry = this.broadcastManager.GetTransaction(inv.Hash);
                if (txEntry != null)
                {
                    if (txEntry.State != State.CantBroadcast)
                    {
                        node.SendMessage(new TxPayload(txEntry.Transaction));
                        if (txEntry.State == State.ToBroadcast)
                        {
                            this.broadcastManager.AddOrUpdate(txEntry.Transaction, State.Broadcasted);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived += this.AttachedNode_MessageReceivedAsync;
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived -= this.AttachedNode_MessageReceivedAsync;
        }
    }
}
