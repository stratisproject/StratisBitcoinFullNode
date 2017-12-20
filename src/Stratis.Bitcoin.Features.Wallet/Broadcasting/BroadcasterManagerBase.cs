using System;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public abstract class BroadcasterManagerBase : IBroadcasterManager
    {
        public event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        /// <summary> Connection manager for managing node connections.</summary>
        private readonly IConnectionManager connectionManager;

        public BroadcasterManagerBase(IConnectionManager connectionManager)
        {
            Guard.NotNull(connectionManager, nameof(connectionManager));

            this.connectionManager = connectionManager;
            this.Broadcasts = new ConcurrentHashSet<TransactionBroadcastEntry>();
        }

        public void OnTransactionStateChanged(TransactionBroadcastEntry entry)
        {
            this.TransactionStateChanged?.Invoke(this, entry);
        }

        private ConcurrentHashSet<TransactionBroadcastEntry> Broadcasts { get; }

        public TransactionBroadcastEntry GetTransaction(uint256 transactionHash)
        {
            TransactionBroadcastEntry txEntry = this.Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transactionHash);
            if (txEntry == default(TransactionBroadcastEntry))
                return null;

            return txEntry;
        }

        public void AddOrUpdate(Transaction transaction, State state)
        {
            TransactionBroadcastEntry broadcastEntry = this.Broadcasts.FirstOrDefault(x => x.Transaction.GetHash() == transaction.GetHash());

            if (broadcastEntry == null)
            {
                broadcastEntry = new TransactionBroadcastEntry(transaction, state);
                this.Broadcasts.Add(broadcastEntry);
                this.OnTransactionStateChanged(broadcastEntry);
            }
            else if (broadcastEntry.State != state)
            {
                broadcastEntry.State = state;
                this.OnTransactionStateChanged(broadcastEntry);
            }
        }

        public abstract Task BroadcastTransactionAsync(Transaction transaction);

        /// <summary>
        /// Sends transaction to peers.
        /// </summary>
        /// <param name="transaction">Transaction that will be propagated.</param>
        /// <param name="skipHalfOfThePeers">If set to <c>true</c> transaction will be send to all the peers we are connected to. Otherwise it will be sent to half of them.</param>
        protected async Task PropagateTransactionToPeersAsync(Transaction transaction, bool skipHalfOfThePeers = false)
        {
            this.AddOrUpdate(transaction, State.ToBroadcast);

            var invPayload = new InvPayload(transaction);

            var peers = this.connectionManager.ConnectedNodes.ToList();
            int propagateToCount = skipHalfOfThePeers ? (int)Math.Ceiling(peers.Count / 2.0) : peers.Count;

            for (int i = 0; i < propagateToCount; ++i)
                await peers[i].SendMessageAsync(invPayload);
        }

        protected bool IsPropagated(Transaction transaction)
        {
            TransactionBroadcastEntry broadcastEntry = this.GetTransaction(transaction.GetHash());
            return broadcastEntry != null && broadcastEntry.State == State.Propagated;
        }
    }
}
