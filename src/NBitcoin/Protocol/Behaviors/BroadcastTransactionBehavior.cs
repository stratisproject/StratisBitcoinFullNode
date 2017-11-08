using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace NBitcoin.Protocol.Behaviors
{
    public enum BroadcastState
    {
        NotSent,
        Announced,
        Broadcasted,
        Rejected,
        Accepted
    }

    public delegate void TransactionBroadcastedDelegate(Transaction transaction);
    public delegate void TransactionRejectedDelegate(Transaction transaction, RejectPayload reject);

    public class TransactionBroadcast
    {
        public BroadcastState State { get; internal set; }
        public Transaction Transaction { get; internal set; }
        internal ulong PingValue { get; set; }
        public DateTime AnnouncedTime { get; internal set; }
    }

    public class BroadcastHub
    {
        internal ConcurrentDictionary<uint256, Transaction> BroadcastedTransaction;
        internal ConcurrentDictionary<Node, Node> Nodes;

        public event TransactionBroadcastedDelegate TransactionBroadcasted;
        public event TransactionRejectedDelegate TransactionRejected;

        public IEnumerable<Transaction> BroadcastingTransactions
        {
            get
            {
                return this.BroadcastedTransaction.Values;
            }
        }

        /// <summary>If <c>true</c>, the user need to call BroadcastTransactions to ask to the nodes to broadcast it.</summary>
        public bool ManualBroadcast { get; set; }

        public BroadcastHub()
        {
            this.BroadcastedTransaction = new ConcurrentDictionary<uint256, Transaction>();
            this.Nodes = new ConcurrentDictionary<Node, Node>();
            this.ManualBroadcast = false;
        }

        public static BroadcastHub GetBroadcastHub(Node node)
        {
            return GetBroadcastHub(node.Behaviors);
        }

        public static BroadcastHub GetBroadcastHub(NodeConnectionParameters parameters)
        {
            return GetBroadcastHub(parameters.TemplateBehaviors);
        }

        public static BroadcastHub GetBroadcastHub(NodeBehaviorsCollection behaviors)
        {
            return behaviors.OfType<BroadcastHubBehavior>().Select(c => c.BroadcastHub).FirstOrDefault();
        }

        internal void OnBroadcastTransaction(Transaction transaction)
        {
            BroadcastHubBehavior[] nodes = this.Nodes
                .Select(n => n.Key.Behaviors.Find<BroadcastHubBehavior>())
                .Where(n => n != null)
                .ToArray();

            foreach (BroadcastHubBehavior node in nodes)
            {
                node.BroadcastTransactionCore(transaction);
            }
        }

        internal void OnTransactionRejected(Transaction tx, RejectPayload reject)
        {
            this.TransactionRejected?.Invoke(tx, reject);
        }

        internal void OnTransactionBroadcasted(Transaction tx)
        {
            this.TransactionBroadcasted?.Invoke(tx);
        }

        /// <summary>
        /// Broadcast a transaction on the hub.
        /// </summary>
        /// <param name="transaction">The transaction to broadcast.</param>
        /// <returns>The cause of the rejection or <c>null</c>.</returns>
        public Task<RejectPayload> BroadcastTransactionAsync(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException("transaction");

            TaskCompletionSource<RejectPayload> completion = new TaskCompletionSource<RejectPayload>();
            uint256 hash = transaction.GetHash();
            if (this.BroadcastedTransaction.TryAdd(hash, transaction))
            {
                TransactionBroadcastedDelegate broadcasted = null;
                TransactionRejectedDelegate rejected = null;
                broadcasted = (t) =>
                {
                    if (t.GetHash() == hash)
                    {
                        completion.SetResult(null);
                        this.TransactionRejected -= rejected;
                        this.TransactionBroadcasted -= broadcasted;
                    }
                };

                this.TransactionBroadcasted += broadcasted;

                rejected = (t, r) =>
                {
                    if (r.Hash == hash)
                    {
                        completion.SetResult(r);
                        this.TransactionRejected -= rejected;
                        this.TransactionBroadcasted -= broadcasted;
                    }
                };

                this.TransactionRejected += rejected;
                this.OnBroadcastTransaction(transaction);
            }

            return completion.Task;
        }

        /// <summary>
        /// Ask the nodes in the hub to broadcast transactions in the Hub manually.
        /// </summary>
        public void BroadcastTransactions()
        {
            if (!this.ManualBroadcast)
                throw new InvalidOperationException("ManualBroadcast should be true to call this method");

            BroadcastHubBehavior[] nodes = this.Nodes
                .Select(n => n.Key.Behaviors.Find<BroadcastHubBehavior>())
                .Where(n => n != null)
                .ToArray();

            foreach (BroadcastHubBehavior node in nodes)
            {
                node.AnnounceAll(true);
            }
        }

        public BroadcastHubBehavior CreateBehavior()
        {
            return new BroadcastHubBehavior(this);
        }
    }

    public class BroadcastHubBehavior : NodeBehavior
    {
        private readonly ConcurrentDictionary<uint256, TransactionBroadcast> hashToTransaction;
        private readonly ConcurrentDictionary<ulong, TransactionBroadcast> pingToTransaction;
        public BroadcastHub BroadcastHub { get; private set; }
        private Timer flush;

        public BroadcastHubBehavior()
        {
            this.hashToTransaction = new ConcurrentDictionary<uint256, TransactionBroadcast>();
            this.pingToTransaction = new ConcurrentDictionary<ulong, TransactionBroadcast>();
            this.BroadcastHub = new BroadcastHub();
        }

        public BroadcastHubBehavior(BroadcastHub hub)
        {
            this.BroadcastHub = hub ?? new BroadcastHub();
            foreach (KeyValuePair<uint256, Transaction> tx in this.BroadcastHub.BroadcastedTransaction)
            {
                this.hashToTransaction.TryAdd(tx.Key, new TransactionBroadcast()
                {
                    State = BroadcastState.NotSent,
                    Transaction = tx.Value
                });
            }
        }

        TransactionBroadcast GetTransaction(uint256 hash, bool remove)
        {
            TransactionBroadcast result;

            if (remove)
            {
                if (this.hashToTransaction.TryRemove(hash, out result))
                {
                    TransactionBroadcast unused;
                    this.pingToTransaction.TryRemove(result.PingValue, out unused);
                }
            }
            else
            {
                this.hashToTransaction.TryGetValue(hash, out result);
            }

            return result;
        }

        TransactionBroadcast GetTransaction(ulong pingValue, bool remove)
        {
            TransactionBroadcast result;

            if (remove)
            {
                if (this.pingToTransaction.TryRemove(pingValue, out result))
                {
                    TransactionBroadcast unused;
                    this.hashToTransaction.TryRemove(result.Transaction.GetHash(), out unused);
                }
            }
            else
            {
                this.pingToTransaction.TryGetValue(pingValue, out result);
            }

            return result;
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if (node.State == NodeState.HandShaked)
            {
                this.BroadcastHub.Nodes.TryAdd(node, node);
                this.AnnounceAll();
            }
        }

        internal void AnnounceAll(bool force = false)
        {
            foreach (KeyValuePair<uint256, TransactionBroadcast> broadcasted in this.hashToTransaction)
            {
                // TODO: Use DateTimeProvider.
                if ((broadcasted.Value.State == BroadcastState.NotSent)
                    || ((DateTime.UtcNow - broadcasted.Value.AnnouncedTime) < TimeSpan.FromMinutes(5.0)))
                    this.Announce(broadcasted.Value, broadcasted.Key, force);
            }
        }


        internal void BroadcastTransactionCore(Transaction transaction)
        {
            var tx = new TransactionBroadcast
            {
                Transaction = transaction ?? throw new ArgumentNullException("transaction"),
                State = BroadcastState.NotSent
            };

            uint256 hash = transaction.GetHash();
            if (this.hashToTransaction.TryAdd(hash, tx))
            {
                this.Announce(tx, hash);
            }
        }

        internal void Announce(TransactionBroadcast tx, uint256 hash, bool force = false)
        {
            if (!force && this.BroadcastHub.ManualBroadcast)
                return;

            Node node = this.AttachedNode;
            if ((node != null) && (node.State == NodeState.HandShaked))
            {
                tx.State = BroadcastState.Announced;
                tx.AnnouncedTime = DateTime.UtcNow;
                node.SendMessageAsync(new InvPayload(InventoryType.MSG_TX, hash)).ConfigureAwait(false);
            }
        }

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
            this.flush = new Timer(o =>
            {
                this.AnnounceAll();
            }, null, 0, (int)TimeSpan.FromMinutes(10).TotalMilliseconds);
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived -= AttachedNode_MessageReceived;

            Node unused;
            this.BroadcastHub.Nodes.TryRemove(this.AttachedNode, out unused);
            this.flush.Dispose();
        }

        void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is InvPayload invPayload)
            {
                foreach (uint256 hash in invPayload.Where(i => i.Type == InventoryType.MSG_TX).Select(i => i.Hash))
                {
                    TransactionBroadcast tx = GetTransaction(hash, true);
                    if (tx != null)
                        tx.State = BroadcastState.Accepted;

                    Transaction unused;
                    if (this.BroadcastHub.BroadcastedTransaction.TryRemove(hash, out unused))
                    {
                        this.BroadcastHub.OnTransactionBroadcasted(tx.Transaction);
                    }
                }
            }

            if ((message.Message.Payload is RejectPayload reject) && (reject.Message == "tx"))
            {
                TransactionBroadcast tx = GetTransaction(reject.Hash, true);
                if (tx != null)
                    tx.State = BroadcastState.Rejected;

                Transaction tx2;
                if (this.BroadcastHub.BroadcastedTransaction.TryRemove(reject.Hash, out tx2))
                {
                    this.BroadcastHub.OnTransactionRejected(tx2, reject);
                }

            }

            if (message.Message.Payload is GetDataPayload getData)
            {
                foreach (InventoryVector inventory in getData.Inventory.Where(i => i.Type == InventoryType.MSG_TX))
                {
                    TransactionBroadcast tx = GetTransaction(inventory.Hash, false);
                    if (tx != null)
                    {
                        tx.State = BroadcastState.Broadcasted;
                        var ping = new PingPayload();
                        tx.PingValue = ping.Nonce;
                        this.pingToTransaction.TryAdd(tx.PingValue, tx);
                        node.SendMessageAsync(new TxPayload(tx.Transaction));
                        node.SendMessageAsync(ping);
                    }
                }
            }

            if (message.Message.Payload is PongPayload pong)
            {
                TransactionBroadcast tx = GetTransaction(pong.Nonce, true);
                if (tx != null)
                {
                    tx.State = BroadcastState.Accepted;
                    Transaction unused;
                    if (this.BroadcastHub.BroadcastedTransaction.TryRemove(tx.Transaction.GetHash(), out unused))
                    {
                        this.BroadcastHub.OnTransactionBroadcasted(tx.Transaction);
                    }
                }
            }
        }

        public override object Clone()
        {
            return new BroadcastHubBehavior(this.BroadcastHub);
        }

        public IEnumerable<TransactionBroadcast> Broadcasts
        {
            get
            {
                return this.hashToTransaction.Values;
            }
        }
    }
}