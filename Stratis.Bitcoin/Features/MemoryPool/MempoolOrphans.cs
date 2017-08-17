using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Manages memory pool orphan transactions.
    /// </summary>
    public class MempoolOrphans
    {
        #region Fields

        /// <summary>Expiration time for orphan transactions in seconds.</summary>
        private const long OrphanTxExpireTime = 20 * 60;

        /// <summary>Default for -maxorphantx, maximum number of orphan transactions kept in memory.</summary>
        public const int DefaultMaxOrphanTransactions = 100;

        /// <summary>Minimum time between orphan transactions expire time checks in seconds.</summary>
        public const int OrphanTxExpireInterval = 5 * 60;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        private readonly TxMempool memPool;

        /// <summary>Chain of block headers.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Node notifications available to subscribe to.</summary>
        private readonly Signals.Signals signals;

        /// <summary>Proof of work consensus validator used for validating orphan transactions.</summary>
        private readonly PowConsensusValidator consensusValidator;

        /// <summary>Coin view of the memory pool.</summary>
        private readonly CoinView coinView;

        /// <summary>Date and time information provider.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Settings from the node.</summary>
        private readonly NodeSettings nodeArgs;

        /// <summary>Logger for the memory pool.</summary>
        private readonly ILogger mempoolLogger;

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

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs a memory pool orphan manager object.
        /// </summary>
        /// <param name="mempoolLock">A lock for managing asynchronous access to memory pool.</param>
        /// <param name="memPool">Transaction memory pool for managing transactions in the memory pool.</param>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="signals">Node notifications available to subscribe to.</param>
        /// <param name="validator">Memory pool validator for validating transactions.</param>
        /// <param name="consensusValidator">Proof of work consensus validator used for validating orphan transactions.</param>
        /// <param name="coinView">Coin view of the memory pool.</param>
        /// <param name="dateTimeProvider">Date and time information provider.</param>
        /// <param name="nodeArgs">Settings from the node.</param>
        /// <param name="loggerFactory">Factory for creating logger for this object.</param>
        public MempoolOrphans(
            MempoolAsyncLock mempoolLock, 
            TxMempool memPool, 
            ConcurrentChain chain, 
            Signals.Signals signals, 
            IMempoolValidator validator, 
            PowConsensusValidator consensusValidator, 
            CoinView coinView, 
            IDateTimeProvider dateTimeProvider, 
            NodeSettings nodeArgs,
            ILoggerFactory loggerFactory)
        {
            this.MempoolLock = mempoolLock;
            this.memPool = memPool;
            this.chain = chain;
            this.signals = signals;
            this.consensusValidator = consensusValidator;
            this.coinView = coinView;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeArgs = nodeArgs;
            this.Validator = validator;

            this.mapOrphanTransactions = new Dictionary<uint256, OrphanTx>();
            this.mapOrphanTransactionsByPrev = new Dictionary<OutPoint, List<OrphanTx>>(); // OutPoint already correctly implements equality compare
            this.recentRejects = new Dictionary<uint256, uint256>();
            this.hashRecentRejectsChainTip = uint256.Zero;
            this.mempoolLogger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        #endregion

        #region Properties

        /// <summary>A lock for managing asynchronous access to memory pool.</summary>
        public MempoolAsyncLock MempoolLock { get; }

        /// <summary>Memory pool validator for validating transactions.</summary>
        public IMempoolValidator Validator { get; } // public for testing

        #endregion

        #region Operations

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
        };

        /// <summary>
        /// Gets a list of all the orphan transactions.
        /// </summary>
        /// <returns>A list of orphan transactions.</returns>
        public List<OrphanTx> OrphansList() // for testing
        {
            return this.mapOrphanTransactions.Values.ToList();
        }

        /// <summary>
        /// Whether the transaction id is already present in the list of orphans.
        /// </summary>
        /// <param name="trxid">transaction id to search for.</param>
        /// <returns>Whether the transaction id is present.</returns>
        public async Task<bool> AlreadyHave(uint256 trxid)
        {
            if (this.chain.Tip.HashBlock != this.hashRecentRejectsChainTip)
            {
                await this.MempoolLock.WriteAsync(() =>
                {
                    // If the chain tip has changed previously rejected transactions
                    // might be now valid, e.g. due to a nLockTime'd tx becoming valid,
                    // or a double-spend. Reset the rejects filter and give those
                    // txs a second chance.
                    this.hashRecentRejectsChainTip = this.chain.Tip.HashBlock;
                    this.recentRejects.Clear();
                });
            }

            // Use pcoinsTip->HaveCoinsInCache as a quick approximation to exclude
            // requesting or processing some txs which have already been included in a block
            return await this.MempoolLock.ReadAsync(() =>
                                this.recentRejects.ContainsKey(trxid) ||
                                this.memPool.Exists(trxid) ||
                                this.mapOrphanTransactions.ContainsKey(trxid));
        }

        /// <summary>
        /// Processes orphan transactions.
        /// Executed when receive a new transaction through MempoolBehavior.
        /// </summary>
        /// <param name="behavior">Memory pool behavior that received new transaction.</param>
        /// <param name="tx">The new transaction received.</param>
        public async Task ProcessesOrphans(MempoolBehavior behavior, Transaction tx)
        {
            Queue<OutPoint> vWorkQueue = new Queue<OutPoint>();
            List<uint256> vEraseQueue = new List<uint256>();

            uint256 trxHash = tx.GetHash();
            for (int index = 0; index < tx.Outputs.Count; index++)
                vWorkQueue.Enqueue(new OutPoint(trxHash, index));

            // Recursively process any orphan transactions that depended on this one
            List<ulong> setMisbehaving = new List<ulong>();
            while (vWorkQueue.Any())
            {
                // mapOrphanTransactionsByPrev.TryGet() does a .ToList() to take a new collection 
                // of orphans as this collection may be modifed later by anotehr thread
                List<OrphanTx> itByPrev = await this.MempoolLock.ReadAsync(() => this.mapOrphanTransactionsByPrev.TryGet(vWorkQueue.Dequeue())?.ToList());
                if (itByPrev == null)
                    continue;

                foreach (OrphanTx mi in itByPrev)
                {
                    Transaction orphanTx = mi.Tx; //->second.tx;
                    uint256 orphanHash = orphanTx.GetHash();
                    ulong fromPeer = mi.NodeId;// (*mi)->second.fromPeer;

                    if (setMisbehaving.Contains(fromPeer))
                        continue;

                    // Use a dummy CValidationState so someone can't setup nodes to counter-DoS based on orphan
                    // resolution (that is, feeding people an invalid transaction based on LegitTxX in order to get
                    // anyone relaying LegitTxX banned)
                    MempoolValidationState stateDummy = new MempoolValidationState(true);
                    if (await this.Validator.AcceptToMemoryPool(stateDummy, orphanTx))
                    {
                        this.mempoolLogger.LogInformation($"accepted orphan tx {orphanHash}");
                        await behavior.RelayTransaction(orphanTx.GetHash());
                        this.signals.SignalTransaction(orphanTx);
                        for (int index = 0; index < orphanTx.Outputs.Count; index++)
                            vWorkQueue.Enqueue(new OutPoint(orphanHash, index));
                        vEraseQueue.Add(orphanHash);
                    }
                    else if (!stateDummy.MissingInputs)
                    {
                        int nDos = 0;
                        if (stateDummy.IsInvalid && nDos > 0)
                        {
                            // Punish peer that gave us an invalid orphan tx
                            //Misbehaving(fromPeer, nDos);
                            setMisbehaving.Add(fromPeer);
                            this.mempoolLogger.LogInformation($"invalid orphan tx {orphanHash}");
                        }
                        // Has inputs but not accepted to mempool
                        // Probably non-standard or insufficient fee/priority
                        this.mempoolLogger.LogInformation($"removed orphan tx {orphanHash}");
                        vEraseQueue.Add(orphanHash);
                        if (!orphanTx.HasWitness && !stateDummy.CorruptionPossible)
                        {
                            // Do not use rejection cache for witness transactions or
                            // witness-stripped transactions, as they can have been malleated.
                            // See https://github.com/bitcoin/bitcoin/issues/8279 for details.
                            await this.MempoolLock.WriteAsync(() => this.recentRejects.TryAdd(orphanHash, orphanHash));
                        }
                    }
                    this.memPool.Check(new MempoolCoinView(this.coinView, this.memPool, this.MempoolLock, this.Validator));
                }
            }

            foreach (uint256 hash in vEraseQueue)
                await this.EraseOrphanTx(hash);
        }

        /// <summary>
        /// Adds transaction to orphan list after checking parents and inputs.
        /// Executed if new transaction has been validated to having missing inputs.
        /// If parents for this transaction have all been rejected than reject this transaction.
        /// </summary>
        /// <param name="from">Source node for transaction.</param>
        /// <param name="tx">Transaction to add.</param>
        /// <returns>Whether the transaction was added to orphans.</returns>
        public async Task<bool> ProcessesOrphansMissingInputs(Node from, Transaction tx)
        {
            // It may be the case that the orphans parents have all been rejected
            var rejectedParents = await this.MempoolLock.ReadAsync(() =>
            {
                return tx.Inputs.Any(txin => this.recentRejects.ContainsKey(txin.PrevOut.Hash));
            });

            if (rejectedParents)
            {
                this.mempoolLogger.LogInformation($"not keeping orphan with rejected parents {tx.GetHash()}");
                return false;
            }

            foreach (var txin in tx.Inputs)
            {
                // TODO: this goes in the RelayBehaviour
                //CInv _inv(MSG_TX | nFetchFlags, txin.prevout.hash);
                //behavior.AttachedNode.Behaviors.Find<RelayBehaviour>() pfrom->AddInventoryKnown(_inv);
                //if (!await this.AlreadyHave(txin.PrevOut.Hash))
                //	from. pfrom->AskFor(_inv);
            }
            var ret = await this.AddOrphanTx(from.PeerVersion.Nonce, tx);

            // DoS prevention: do not allow mapOrphanTransactions to grow unbounded
            int nMaxOrphanTx = this.nodeArgs.Mempool.MaxOrphanTx;
            int nEvicted = await this.LimitOrphanTxSize(nMaxOrphanTx);
            if (nEvicted > 0)
                this.mempoolLogger.LogInformation($"mapOrphan overflow, removed {nEvicted} tx");

            return ret;
        }

        /// <summary>
        /// Limit the orphan transaction list by a max size.
        /// First prune expired orphan pool entries within the sweep period.
        /// If further pruning is required to get to limit, then evict randomly.
        /// </summary>
        /// <param name="maxOrphanTx">Size to limit the orphan transactions to.</param>
        /// <returns>The number of transactions evicted.</returns>
        public async Task<int> LimitOrphanTxSize(int maxOrphanTx)
        {
            int nEvicted = 0;
            var nNow = this.dateTimeProvider.GetTime();
            if (this.nNextSweep <= nNow)
            {
                // Sweep out expired orphan pool entries:
                int nErased = 0;
                long nMinExpTime = nNow + OrphanTxExpireTime - OrphanTxExpireInterval;

                List<OrphanTx> orphansValues = await this.MempoolLock.ReadAsync(() => this.mapOrphanTransactions.Values.ToList());
                foreach (OrphanTx maybeErase in orphansValues) // create a new list as this will be removing items from the dictionary
                {
                    if (maybeErase.TimeExpire <= nNow)
                    {
                        nErased += await this.EraseOrphanTx(maybeErase.Tx.GetHash()) ? 1 : 0;
                    }
                    else
                    {
                        nMinExpTime = Math.Min(maybeErase.TimeExpire, nMinExpTime);
                    }
                }
                // Sweep again 5 minutes after the next entry that expires in order to batch the linear scan.
                this.nNextSweep = nMinExpTime + OrphanTxExpireInterval;
                if (nErased > 0)
                    this.mempoolLogger.LogInformation($"Erased {nErased} orphan tx due to expiration");
            }
            await await this.MempoolLock.ReadAsync(async () =>
            {
                while (this.mapOrphanTransactions.Count > maxOrphanTx)
                {
                    // Evict a random orphan:
                    int randomCount = this.random.Next(this.mapOrphanTransactions.Count);
                    uint256 erase = this.mapOrphanTransactions.ElementAt(randomCount).Key;
                    await this.EraseOrphanTx(erase); // this will split the loop between the concurrent and exclusive scheduler
                    ++nEvicted;
                }
            });

            return nEvicted;
        }

        /// <summary>
        /// Add an orphan transaction to the orphan pool.
        /// </summary>
        /// <param name="nodeId">Node id of the source node.</param>
        /// <param name="tx">The transaction to add.</param>
        /// <returns>Whether the orphan transaction was added.</returns>
        public Task<bool> AddOrphanTx(ulong nodeId, Transaction tx)
        {
            return this.MempoolLock.WriteAsync(() =>
            {
                uint256 hash = tx.GetHash();
                if (this.mapOrphanTransactions.ContainsKey(hash))
                    return false;

                // Ignore big transactions, to avoid a
                // send-big-orphans memory exhaustion attack. If a peer has a legitimate
                // large transaction with a missing parent then we assume
                // it will rebroadcast it later, after the parent transaction(s)
                // have been mined or received.
                // 100 orphans, each of which is at most 99,999 bytes big is
                // at most 10 megabytes of orphans and somewhat more byprev index (in the worst case):
                int sz = MempoolValidator.GetTransactionWeight(tx, this.Validator.ConsensusOptions);
                if (sz >= this.consensusValidator.ConsensusOptions.MAX_STANDARD_TX_WEIGHT)
                {
                    this.mempoolLogger.LogInformation($"ignoring large orphan tx (size: {sz}, hash: {hash})");
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
                    foreach (var txin in tx.Inputs)
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
                this.mempoolLogger.LogInformation($"stored orphan tx {hash} (mapsz {orphanSize} outsz {this.mapOrphanTransactionsByPrev.Count})");
                this.Validator.PerformanceCounter.SetMempoolOrphanSize(orphanSize);

                return true;
            });
        }

        /// <summary>
        /// Erase an specific transaction from orphan pool.
        /// </summary>
        /// <param name="hash">hash of the transaction.</param>
        /// <returns>Whether erased.</returns>
        private Task<bool> EraseOrphanTx(uint256 hash)
        {
            return this.MempoolLock.WriteAsync(() =>
            {
                OrphanTx it = this.mapOrphanTransactions.TryGet(hash);
                if (it == null)
                    return false;
                foreach (TxIn txin in it.Tx.Inputs)
                {
                    List<OrphanTx> itPrev = this.mapOrphanTransactionsByPrev.TryGet(txin.PrevOut);
                    if (itPrev == null)
                        continue;
                    itPrev.Remove(it);
                    if (!itPrev.Any())
                        this.mapOrphanTransactionsByPrev.Remove(txin.PrevOut);
                }
                this.mapOrphanTransactions.Remove(hash);
                return true;
            });
        }

        /// <summary>
        /// Erase all orphans for a specific peer node.
        /// </summary>
        /// <param name="peer">Peer node id</param>
        public Task EraseOrphansFor(ulong peer)
        {
            return this.MempoolLock.ReadAsync(async () =>
            {
                int erased = 0;
                foreach (OrphanTx erase in this.mapOrphanTransactions.Values.Where(w => w.NodeId == peer).ToList())
                    erased += await this.EraseOrphanTx(erase.Tx.GetHash()) ? 1 : 0;

                if (erased > 0)
                    this.mempoolLogger.LogInformation($"Erased {erased} orphan tx from peer {peer}");

                //return true;
            }).Unwrap();
        }

        #endregion
    }
}
