using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Broadcasting
{
    public class BroadcasterBehavior : NodeBehavior
    {
        protected readonly IBroadcasterManager manager;

        /// <summary>Instance logger for the memory pool component.</summary>
        protected readonly ILogger logger;

        public BroadcasterBehavior(
            IBroadcasterManager manager,
            ILogger logger)
        {
            this.logger = logger;
            this.manager = manager;
        }

        public BroadcasterBehavior(
            IBroadcasterManager manager,
            ILoggerFactory loggerFactory)
            : this(manager, loggerFactory.CreateLogger(typeof(BroadcasterBehavior).FullName))
        {

        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BroadcasterBehavior(this.manager, this.logger);
        }

        /// <summary>
        /// Handler for processing incoming message from node.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        /// <remarks>
        /// TODO: Fix the exception handling of the async event.
        /// </remarks>
        protected async void AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(node, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException opx)
            {
                if (!opx.CancellationToken.IsCancellationRequested)
                    if (this.AttachedNode?.IsConnected ?? false)
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
        /// Handles the following message payloads: TxPayload, MempoolPayload, GetDataPayload, InvPayload.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        protected Task ProcessMessageAsync(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is GetDataPayload getDataPayload)
            {
                ProcessGetDataPayload(node, getDataPayload);
                return Task.CompletedTask;
            }

            if (message.Message.Payload is InvPayload invPayload)
            {
                ProcessInvPayload(invPayload);
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        private void ProcessInvPayload(InvPayload invPayload)
        {
            // if node has tx we broadcasted
            foreach (var inv in invPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                var txEntry = this.manager.GetTransaction(inv.Hash);
                if (txEntry != null)
                {
                    this.manager.AddOrUpdate(txEntry.Transaction, State.Propagated);
                }
            }
        }

        protected void ProcessGetDataPayload(Node node, GetDataPayload getDataPayload)
        {
            // if node asks for tx we want to broadcast
            foreach (var inv in getDataPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
            {
                var txEntry = this.manager.GetTransaction(inv.Hash);
                if (txEntry != null)
                {
                    if (txEntry.State != State.CantBroadcast)
                    {
                        node.SendMessage(new TxPayload(txEntry.Transaction));
                        if (txEntry.State == State.ToBroadcast)
                        {
                            this.manager.AddOrUpdate(txEntry.Transaction, State.Broadcasted);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceivedAsync;
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceivedAsync;
        }
    }
}