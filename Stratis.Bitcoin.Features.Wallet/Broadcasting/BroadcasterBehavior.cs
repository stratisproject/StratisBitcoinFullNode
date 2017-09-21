using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public class BroodcasterBehavior : NodeBehavior
    {            
        private readonly BroadcastState broadcastState;

        private readonly IWalletManager walletManager;

        /// <summary>Instance logger for the memory pool component.</summary>
        private readonly ILogger logger;

        public BroodcasterBehavior(
            BroadcastState broadcastState,
            ILogger logger,
            IWalletManager walletManager)
        {
            this.logger = logger;
            this.broadcastState = broadcastState;
            this.walletManager = walletManager;
        }

        public BroodcasterBehavior(
            BroadcastState broadcastState,
            ILoggerFactory loggerFactory,
            IWalletManager walletManager)
            : this(broadcastState, loggerFactory.CreateLogger(typeof(BroodcasterBehavior).FullName), walletManager)
        {
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BroodcasterBehavior(this.broadcastState, this.logger, this.walletManager);
        }

        /// <summary>
        /// Handler for processing incoming message from node.
        /// </summary>
        /// <param name="node">Node sending the message.</param>
        /// <param name="message">Incoming message.</param>
        /// <remarks>
        /// TODO: Fix the exception handling of the async event.
        /// </remarks>
        private async void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            try
            {
                await this.AttachedNode_MessageReceivedAsync(node, message).ConfigureAwait(false);
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
        private Task AttachedNode_MessageReceivedAsync(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is GetDataPayload getDataPayload)
            {
                // if node asks for tx we want to broadcast
                foreach (var inv in getDataPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
                {
                    Transaction transaction = this.broadcastState.Broadcasts.FirstOrDefault(x => x.GetHash() == inv.Hash);
                    if(transaction != default(Transaction))
                    {
                        node.SendMessage(new TxPayload(transaction));
                    }
                }
                return Task.CompletedTask;
            }

            if (message.Message.Payload is InvPayload invPayload)
            {
                // if node has tx we broadcasted
                foreach (var inv in invPayload.Inventory.Where(x => x.Type == InventoryType.MSG_TX))
                {
                    Transaction transaction = this.broadcastState.Broadcasts.FirstOrDefault(x => x.GetHash() == inv.Hash);
                    if (transaction != default(Transaction))
                    {
                        this.broadcastState.Broadcasts.TryRemove(transaction);
                        this.walletManager.ProcessTransaction(transaction);
                    }
                }
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.AttachedNode.MessageReceived += this.AttachedNode_MessageReceived;
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;
        }
    }
}
