﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ConcurrentCollections;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Broadcasting
{
    public abstract class BroadcastManagerBase : IBroadcastManager
    {
        public event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        protected readonly IConnectionManager connectionManager;

        private ConcurrentHashSet<TransactionBroadcastEntry> Broadcasts { get; }

        public BroadcastManagerBase(IConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
            this.Broadcasts = new ConcurrentHashSet<TransactionBroadcastEntry>();
        }
        public void OnTransactionStateChanged(TransactionBroadcastEntry entry)
        {
            this.TransactionStateChanged?.Invoke(this, entry);
        }

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
            else
            {
                var oldState = broadcastEntry.State;
                broadcastEntry.State = state;

                if (oldState != broadcastEntry.State)
                    this.OnTransactionStateChanged(broadcastEntry);
            }
        }

        public abstract Task<Success> TryBroadcastAsync(Transaction transaction);

        protected void PropagateTransactionToPeers(Transaction transaction)
        {
            this.AddOrUpdate(transaction, State.ToBroadcast);

            // ask half of the peers if they're interested in our transaction
            var invPayload = new InventoryPayload(transaction);

            foreach (NetworkPeer networkPeer in this.connectionManager.ConnectedNodes)
            {
                //TODO run in several threads
                networkPeer.SendMessageAsync(invPayload).GetAwaiter().GetResult();
            }
        }

        protected bool IsPropagated(Transaction transaction)
        {
            TransactionBroadcastEntry broadcastEntry = this.GetTransaction(transaction.GetHash());
            return broadcastEntry != null && broadcastEntry.State == State.Propagated;
        }
    }
}
