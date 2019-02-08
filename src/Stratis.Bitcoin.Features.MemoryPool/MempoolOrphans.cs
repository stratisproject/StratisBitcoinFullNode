using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Manages memory pool orphan transactions.
    /// </summary>
    public class MempoolOrphans
    {
        /// <summary>Expiration time for orphan transactions in seconds.</summary>
        private const long OrphanTxExpireTime = 20 * 60;

        /// <summary>Default for -maxorphantx, maximum number of orphan transactions kept in memory.</summary>
        public const int DefaultMaxOrphanTransactions = 100;

        /// <summary>Minimum time between orphan transactions expire time checks in seconds.</summary>
        public const int OrphanTxExpireInterval = 5 * 60;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Node notifications available to subscribe to.</summary>
        private readonly Signals.ISignals signals;

        /// <summary>Coin view of the memory pool.</summary>
        private readonly ICoinView coinView;

        /// <summary>Date and time information provider.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Settings from the memory pool.</summary>
        private readonly MempoolSettings mempoolSettings;

        /// <summary>Manages the memory pool transactions.</summary>
        private readonly MempoolManager mempoolManager;

        /// <summary>Instance logger for the memory pool.</summary>
        private readonly ILogger logger;

        /// <summary>Dictionary of orphan transactions keyed by transaction hash.</summary>
        private readonly Dictionary<uint256, OrphanTx> mapOrphanTransactions;

        /// <summary>Dictionary of orphan transactions keyed by transaction output.</summary>
        private readonly Dictionary<OutPoint, List<OrphanTx>> mapOrphanTransactionsByPrev;

        /// <summary>Dictionary of recent transaction rejects keyed by transaction hash</summary>
        private readonly Dictionary<uint256, uint256> recentRejects;

        /// <summary>Time of next sweep to purge expired orphan transactions.</summary>
        private long nNextSweep;

        /// <summary> Object for generating random numbers used for randomly purging orphans.</summary>
        private readonly Random random = new Random();

        /// <summary>Location on chain when rejects are validated.</summary>
        private uint256 hashRecentRejectsChainTip;

        /// <summary>Lock object for locking access to local collections.</summary>
        private readonly object lockObject;

        public MempoolOrphans(
            ConcurrentChain chain,
            Signals.ISignals signals,
            IMempoolValidator validator,
            ICoinView coinView,
            IDateTimeProvider dateTimeProvider,
            MempoolSettings mempoolSettings,
            ILoggerFactory loggerFactory,
            MempoolManager mempoolManager)
        {
            this.chain = chain;
            this.signals = signals;
            this.coinView = coinView;
            this.dateTimeProvider = dateTimeProvider;
            this.mempoolSettings = mempoolSettings;
            this.mempoolManager = mempoolManager;
            this.Validator = validator;

            this.mapOrphanTransactions = new Dictionary<uint256, OrphanTx>();
            this.mapOrphanTransactionsByPrev = new Dictionary<OutPoint, List<OrphanTx>>(); // OutPoint already correctly implements equality compare
            this.recentRejects = new Dictionary<uint256, uint256>();
            this.hashRecentRejectsChainTip = uint256.Zero;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.lockObject = new object();
        }

        /// <summary>Memory pool validator for validating transactions.</summary>
        public IMempoolValidator Validator { get; }

        /// <summary>
        /// Object representing an orphan transaction information.
        /// When modifying, adapt the copy of this definition in tests/DoS_tests.
        /// </summary>
        public class OrphanTx
        {
            /// <summary>The orphan transaction.</summary>
            public Transaction Tx;

            /// <summary>The id of the node that sent this transaction.</summary>
            public ulong NodeId;

            /// <summary>The time when this orphan transaction will expire.</summary>
            public long TimeExpire;
        }

        /// <summary>
        /// Gets a list of all the orphan transactions.
        /// </summary>
        /// <returns>A list of orphan transactions.</returns>
        public List<OrphanTx> OrphansList() // for testing
        {
            List<OrphanTx> result;
            lock (this.lockObject)
            {
                result = this.mapOrphanTransactions.Values.ToList();
            }

            return result;
        }

        /// <summary>
        /// Orphan list count.
        /// </summary>
        public int OrphansCount()
        {
            int result;
            lock (this.lockObject)
            {
                result = this.mapOrphanTransactions.Count;
            }

            return result;
        }

        /// <summary>
        /// Remove transactions form the orphan list.
        /// </summary>
        public void RemoveForBlock(List<Transaction> transactionsToRemove)
        {
            lock (this.lockObject)
            {
                foreach (Transaction transaction in transactionsToRemove)
                {
                    this.EraseOrphanTxLock(transaction.GetHash());
                }
            }
        }

        /// <summary>
        /// Whether the transaction id is already present in the list of orphans.
        /// </summary>
        /// <param name="trxid">transaction id to search for.</param>
        /// <returns>Whether the transaction id is present.</returns>
        public async Task<bool> AlreadyHaveAsync(uint256 trxid)
        {
            // Use pcoinsTip->HaveCoinsInCache as a quick approximation to exclude
            // requesting or processing some txs which have already been included in a block
            bool isTxPresent = false;
            lock(this.lockObject)
            {
                if (this.chain.Tip.HashBlock != this.hashRecentRejectsChainTip)
                {
                    // If the chain tip has changed previously rejected transactions
                    // might be now valid, e.g. due to a nLockTime'd tx becoming valid,
                    // or a double-spend. Reset the rejects filter and give those
                    // txs a second chance.
                    this.logger.LogTrace("Executing task to clear rejected transactions.");
                    this.hashRecentRejectsChainTip = this.chain.Tip.HashBlock;
                    this.recentRejects.Clear();
                }

                isTxPresent = this.recentRejects.ContainsKey(trxid) || this.mapOrphanTransactions.ContainsKey(trxid);
            }

            if (!isTxPresent)
            {
                isTxPresent = await this.mempoolManager.ExistsAsync(trxid).ConfigureAwait(false);
            }

            return isTxPresent;
        }

        /// <summary>
        /// Processes orphan transactions.
        /// Executed when receive a new transaction through MempoolBehavior.
        /// </summary>
        /// <param name="behavior">Memory pool behavior that received new transaction.</param>
        /// <param name="tx">The new transaction received.</param>
        public async Task ProcessesOrphansAsync(MempoolBehavior behavior, Transaction tx)
        {
            var workQueue = new Queue<OutPoint>();
            var eraseQueue = new List<uint256>();

            uint256 trxHash = tx.GetHash();
            for (int index = 0; index < tx.Outputs.Count; index++)
                workQueue.Enqueue(new OutPoint(trxHash, index));

            // Recursively process any orphan transactions that depended on this one
            var setMisbehaving = new List<ulong>();
            while (workQueue.Any())
            {
                List<OrphanTx> itByPrev = null;
                lock (this.lockObject)
                {
                    List<OrphanTx> prevOrphans = this.mapOrphanTransactionsByPrev.TryGet(workQueue.Dequeue());

                    if (prevOrphans != null)
                    {
                        // Create a copy of the list so we can manage it outside of the lock.
                        itByPrev = prevOrphans.ToList();
                    }
                }

                if (itByPrev == null)
                    continue;

                foreach (OrphanTx mi in itByPrev)
                {
                    Transaction orphanTx = mi.Tx;
                    uint256 orphanHash = orphanTx.GetHash();
                    ulong fromPeer = mi.NodeId;

                    if (setMisbehaving.Contains(fromPeer))
                        continue;

                    // Use a dummy CValidationState so someone can't setup nodes to counter-DoS based on orphan
                    // resolution (that is, feeding people an invalid transaction based on LegitTxX in order to get
                    // anyone relaying LegitTxX banned)
                    var stateDummy = new MempoolValidationState(true);
                    if (await this.Validator.AcceptToMemoryPool(stateDummy, orphanTx))
                    {
                        this.logger.LogInformation("accepted orphan tx {0}", orphanHash);

                        behavior.RelayTransaction(orphanTx.GetHash());

                        this.signals.OnTransactionReceived.Notify(orphanTx);

                        for (int index = 0; index < orphanTx.Outputs.Count; index++)
                            workQueue.Enqueue(new OutPoint(orphanHash, index));

                        eraseQueue.Add(orphanHash);
                    }
                    else if (!stateDummy.MissingInputs)
                    {
                        int nDos = 0;

                        if (stateDummy.IsInvalid && nDos > 0)
                        {
                            // Punish peer that gave us an invalid orphan tx
                            //Misbehaving(fromPeer, nDos);
                            setMisbehaving.Add(fromPeer);
                            this.logger.LogInformation("invalid orphan tx {0}", orphanHash);
                        }

                        // Has inputs but not accepted to mempool
                        // Probably non-standard or insufficient fee/priority
                        this.logger.LogInformation("removed orphan tx {0}", orphanHash);
                        eraseQueue.Add(orphanHash);
                        if (!orphanTx.HasWitness && !stateDummy.CorruptionPossible)
                        {
                            // Do not use rejection cache for witness transactions or
                            // witness-stripped transactions, as they can have been malleated.
                            // See https://github.com/bitcoin/bitcoin/issues/8279 for details.

                            this.AddToRecentRejects(orphanHash);
                         }
                    }

                    // TODO: implement sanity checks.
                    //this.memPool.Check(new MempoolCoinView(this.coinView, this.memPool, this.MempoolLock, this.Validator));
                }
            }

            if (eraseQueue.Count > 0)
            {
                lock (this.lockObject)
                {
                    foreach (uint256 hash in eraseQueue)
                    {
                        this.EraseOrphanTxLock(hash);
                    }
                }
            }
        }

        /// <summary>
        /// Adds transaction hash to recent rejects.
        /// </summary>
        /// <param name="orphanHash">Hash to add.</param>
        public void AddToRecentRejects(uint256 orphanHash)
        {
            lock (this.lockObject)
            {
                this.recentRejects.TryAdd(orphanHash, orphanHash);
            }
        }

        /// <summary>
        /// Adds transaction to orphan list after checking parents and inputs.
        /// Executed if new transaction has been validated to having missing inputs.
        /// If parents for this transaction have all been rejected than reject this transaction.
        /// </summary>
        /// <param name="from">Source node for transaction.</param>
        /// <param name="tx">Transaction to add.</param>
        /// <returns>Whether the transaction was added to orphans.</returns>
        public bool ProcessesOrphansMissingInputs(INetworkPeer from, Transaction tx)
        {
            // It may be the case that the orphans parents have all been rejected
            bool rejectedParents;
            lock (this.lockObject)
            {
                rejectedParents = tx.Inputs.Any(txin => this.recentRejects.ContainsKey(txin.PrevOut.Hash));
            }

            if (rejectedParents)
            {
                this.logger.LogInformation("not keeping orphan with rejected parents {0}", tx.GetHash());
                this.logger.LogTrace("(-)[REJECT_PARENTS_ORPH]:false");
                return false;
            }

            foreach (TxIn txin in tx.Inputs)
            {
                // TODO: this goes in the RelayBehaviour
                //CInv _inv(MSG_TX | nFetchFlags, txin.prevout.hash);
                //behavior.AttachedNode.Behaviors.Find<RelayBehaviour>() pfrom->AddInventoryKnown(_inv);
                //if (!await this.AlreadyHave(txin.PrevOut.Hash))
                //  from. pfrom->AskFor(_inv);
            }

            bool ret = this.AddOrphanTx(from.PeerVersion.Nonce, tx);

            // DoS prevention: do not allow mapOrphanTransactions to grow unbounded
            int nMaxOrphanTx = this.mempoolSettings.MaxOrphanTx;
            int nEvicted = this.LimitOrphanTxSize(nMaxOrphanTx);
            if (nEvicted > 0)
                this.logger.LogInformation("mapOrphan overflow, removed {0} tx", nEvicted);

            return ret;
        }

        /// <summary>
        /// Limit the orphan transaction list by a max size.
        /// First prune expired orphan pool entries within the sweep period.
        /// If further pruning is required to get to limit, then evict randomly.
        /// </summary>
        /// <param name="maxOrphanTx">Size to limit the orphan transactions to.</param>
        /// <returns>The number of transactions evicted.</returns>
        public int LimitOrphanTxSize(int maxOrphanTx)
        {
            int nEvicted = 0;
            long nNow = this.dateTimeProvider.GetTime();
            if (this.nNextSweep <= nNow)
            {
                // Sweep out expired orphan pool entries:
                int nErased = 0;
                long nMinExpTime = nNow + OrphanTxExpireTime - OrphanTxExpireInterval;

                List<OrphanTx> orphansValues;
                lock (this.lockObject)
                {
                    orphansValues = this.mapOrphanTransactions.Values.ToList();
                }

                foreach (OrphanTx maybeErase in orphansValues) // create a new list as this will be removing items from the dictionary
                {
                    if (maybeErase.TimeExpire <= nNow)
                    {
                        lock (this.lockObject)
                        {
                            nErased += this.EraseOrphanTxLock(maybeErase.Tx.GetHash()) ? 1 : 0;
                        }
                    }
                    else
                    {
                        nMinExpTime = Math.Min(maybeErase.TimeExpire, nMinExpTime);
                    }
                }

                // Sweep again 5 minutes after the next entry that expires in order to batch the linear scan.
                this.nNextSweep = nMinExpTime + OrphanTxExpireInterval;

                if (nErased > 0)
                    this.logger.LogInformation("Erased {0} orphan tx due to expiration", nErased);
            }

            lock (this.lockObject)
            {
                this.logger.LogTrace("Executing task to prune orphan txs to max limit.");
                while (this.mapOrphanTransactions.Count > maxOrphanTx)
                {
                    // Evict a random orphan:
                    int randomCount = this.random.Next(this.mapOrphanTransactions.Count);
                    uint256 erase = this.mapOrphanTransactions.ElementAt(randomCount).Key;
                    this.EraseOrphanTxLock(erase);
                    ++nEvicted;
                }
            }

            return nEvicted;
        }

        /// <summary>
        /// Add an orphan transaction to the orphan pool.
        /// </summary>
        /// <param name="nodeId">Node id of the source node.</param>
        /// <param name="tx">The transaction to add.</param>
        /// <returns>Whether the orphan transaction was added.</returns>
        public bool AddOrphanTx(ulong nodeId, Transaction tx)
        {
            lock (this.lockObject)
            {
                uint256 hash = tx.GetHash();
                if (this.mapOrphanTransactions.ContainsKey(hash))
                {
                    this.logger.LogTrace("(-)[DUP_ORPH]:false");
                    return false;
                }

                // Ignore big transactions, to avoid a
                // send-big-orphans memory exhaustion attack. If a peer has a legitimate
                // large transaction with a missing parent then we assume
                // it will rebroadcast it later, after the parent transaction(s)
                // have been mined or received.
                // 100 orphans, each of which is at most 99,999 bytes big is
                // at most 10 megabytes of orphans and somewhat more byprev index (in the worst case):
                int sz = MempoolValidator.GetTransactionWeight(tx, this.Validator.ConsensusOptions);
                if (sz >= this.chain.Network.Consensus.Options.MaxStandardTxWeight)
                {
                    this.logger.LogInformation("ignoring large orphan tx (size: {0}, hash: {1})", sz, hash);
                    this.logger.LogTrace("(-)[LARGE_ORPH]:false");
                    return false;
                }

                var orphan = new OrphanTx
                {
                    Tx = tx,
                    NodeId = nodeId,
                    TimeExpire = this.dateTimeProvider.GetTime() + OrphanTxExpireTime
                };

                if (this.mapOrphanTransactions.TryAdd(hash, orphan))
                {
                    foreach (TxIn txin in tx.Inputs)
                    {
                        List<OrphanTx> prv = this.mapOrphanTransactionsByPrev.TryGet(txin.PrevOut);
                        if (prv == null)
                        {
                            prv = new List<OrphanTx>();
                            this.mapOrphanTransactionsByPrev.Add(txin.PrevOut, prv);
                        }
                        prv.Add(orphan);
                    }
                }

                int orphanSize = this.mapOrphanTransactions.Count;
                this.logger.LogInformation("stored orphan tx {0} (mapsz {1} outsz {2})", hash, orphanSize, this.mapOrphanTransactionsByPrev.Count);
                this.Validator.PerformanceCounter.SetMempoolOrphanSize(orphanSize);
            }

            return true;
        }

        /// <summary>
        /// Erase an specific transaction from orphan pool.
        /// </summary>
        /// <param name="hash">hash of the transaction.</param>
        /// <returns>Whether erased.</returns>
        private bool EraseOrphanTxLock(uint256 hash)
        {
            OrphanTx orphTx = this.mapOrphanTransactions.TryGet(hash);

            if (orphTx == null)
            {
                this.logger.LogTrace("(-)[NOTFOUND_ORPH]:false");
                return false;
            }

            foreach (TxIn txin in orphTx.Tx.Inputs)
            {
                List<OrphanTx> prevOrphTxList = this.mapOrphanTransactionsByPrev.TryGet(txin.PrevOut);

                if (prevOrphTxList == null)
                    continue;

                prevOrphTxList.Remove(orphTx);

                if (!prevOrphTxList.Any())
                    this.mapOrphanTransactionsByPrev.Remove(txin.PrevOut);
            }

            this.mapOrphanTransactions.Remove(hash);

            int orphanSize = this.mapOrphanTransactions.Count;
            this.Validator.PerformanceCounter.SetMempoolOrphanSize(orphanSize);

            return true;
        }

        /// <summary>
        /// Erase all orphans for a specific peer node.
        /// </summary>
        /// <param name="peerId">Peer node id</param>
        public void EraseOrphansFor(ulong peerId)
        {
            lock (this.lockObject)
            {
                this.logger.LogTrace("Executing task to erase orphan transactions.");

                int erased = 0;

                List<OrphanTx> orphansToErase = this.mapOrphanTransactions.Values.ToList();
                foreach (OrphanTx erase in orphansToErase)
                {
                    if (erase.NodeId == peerId)
                    {
                        erased += this.EraseOrphanTxLock(erase.Tx.GetHash()) ? 1 : 0;
                    }
                }

                if (erased > 0)
                    this.logger.LogInformation("Erased {0} orphan tx from peer {1}", erased, peerId);
            }
        }
    }
}
