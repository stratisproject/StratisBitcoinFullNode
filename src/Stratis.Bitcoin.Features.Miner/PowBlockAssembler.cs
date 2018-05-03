﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    public abstract class BlockAssembler
    {
        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        protected readonly IConsensusLoop ConsensusLoop;

        /// <summary>Provider of date time functions.</summary>
        protected readonly IDateTimeProvider DateTimeProvider;

        /// <summary>Instance logger.</summary>
        protected readonly ILogger Logger;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        protected readonly ITxMempool Mempool;

        /// <summary>Lock for memory pool access.</summary>
        protected readonly MempoolSchedulerLock MempoolLock;

        /// <summary>The current network.</summary>
        protected readonly Network Network;

        /// <summary>Assembler options specific to the assembler e.g. <see cref="AssemblerOptions.BlockMaxSize"/>.</summary>
        protected AssemblerOptions Options;

        protected BlockAssembler(
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network)
        {
            this.ConsensusLoop = consensusLoop;
            this.DateTimeProvider = dateTimeProvider;
            this.Logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Mempool = mempool;
            this.MempoolLock = mempoolLock;
            this.Network = network;
        }

        /// <summary>
        /// Constructs a block template which will be passed to consensus. 
        /// </summary>
        /// <param name="chainTip">Tip of the chain that this instance will work with without touching any shared chain resources.</param>
        /// <param name="scriptPubKey">Script that explains what conditions must be met to claim ownership of a coin.</param>
        /// <returns>The contructed <see cref="BlockTemplate"/>.</returns>
        public abstract BlockTemplate Build(ChainedBlock chainTip, Script scriptPubKey);

        /// <summary>
        /// Tip of the chain that this instance will work with without touching any shared chain resources.
        /// </summary>
        protected ChainedBlock ChainTip { get; set; }
    }

    public class AssemblerOptions
    {
        public long BlockMaxWeight = PowMining.DefaultBlockMaxWeight;

        public long BlockMaxSize = PowMining.DefaultBlockMaxSize;

        public FeeRate BlockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);

        public bool IsProofOfStake = false;
    }

    public class BlockTemplate
    {
        public Block Block;

        public List<Money> VTxFees;

        public List<long> TxSigOpsCost;

        public string CoinbaseCommitment;

        public Money TotalFee;

        public BlockTemplate()
        {
            this.Block = new Block();
            this.VTxFees = new List<Money>();
            this.TxSigOpsCost = new List<long>();
        }
    }

    public class PowBlockAssembler : BlockAssembler
    {
        // Container for tracking updates to ancestor feerate as we include (parent)
        // transactions in a block.
        public class TxMemPoolModifiedEntry
        {
            public TxMemPoolModifiedEntry(TxMempoolEntry entry)
            {
                this.iter = entry;
                this.SizeWithAncestors = entry.SizeWithAncestors;
                this.ModFeesWithAncestors = entry.ModFeesWithAncestors;
                this.SigOpCostWithAncestors = entry.SigOpCostWithAncestors;
            }

            public TxMempoolEntry iter;

            public long SizeWithAncestors;

            public Money ModFeesWithAncestors;

            public long SigOpCostWithAncestors;
        }

        // This matches the calculation in CompareTxMemPoolEntryByAncestorFee,
        // except operating on CTxMemPoolModifiedEntry.
        // TODO: Refactor to avoid duplication of this logic.
        public class CompareModifiedEntry : IComparer<TxMemPoolModifiedEntry>
        {
            public int Compare(TxMemPoolModifiedEntry a, TxMemPoolModifiedEntry b)
            {
                Money f1 = a.ModFeesWithAncestors * b.SizeWithAncestors;
                Money f2 = b.ModFeesWithAncestors * a.SizeWithAncestors;

                if (f1 == f2)
                    return TxMempool.CompareIteratorByHash.InnerCompare(a.iter, b.iter);

                return f1 > f2 ? 1 : -1;
            }
        }

        // A comparator that sorts transactions based on number of ancestors.
        // This is sufficient to sort an ancestor package in an order that is valid
        // to appear in a block.
        public class CompareTxIterByAncestorCount : IComparer<TxMempoolEntry>
        {
            public int Compare(TxMempoolEntry a, TxMempoolEntry b)
            {
                if (a.CountWithAncestors != b.CountWithAncestors)
                    return a.CountWithAncestors < b.CountWithAncestors ? -1 : 1;

                return TxMempool.CompareIteratorByHash.InnerCompare(a, b);
            }
        }

        private const long TicksPerMicrosecond = 10;

        // Limit the number of attempts to add transactions to the block when it is
        // close to full; this is just a simple heuristic to finish quickly if the
        // mempool has a lot of entries.
        private int MaxConsecutiveAddTransactionFailures = 1000;

        // Unconfirmed transactions in the memory pool often depend on other
        // transactions in the memory pool. When we select transactions from the
        // pool, we select by highest fee rate of a transaction combined with all
        // its ancestors.
        private static long lastBlockTx = 0;

        private static long lastBlockSize = 0;

        private static long lastBlockWeight = 0;

        private static long medianTimePast;

        // The constructed block template.
        protected BlockTemplate blockTemplate;

        // A convenience pointer that always refers to the CBlock in pblocktemplate.
        protected Block block;

        // Configuration parameters for the block size.
        private bool includeWitness;

        private uint blockMaxWeight, blockMaxSize;

        private bool needSizeAccounting;

        private FeeRate blockMinFeeRate;

        // Information on the current status of the block.
        private long blockWeight;

        private long blockSize;

        private long blockTx;

        private long blockSigOpsCost;

        public Money fees;

        protected TxMempool.SetEntries inBlock;

        protected Transaction coinbase;

        // Chain context for the block.
        protected int height;

        private long lockTimeCutoff;

        protected Script scriptPubKey;

        public PowBlockAssembler(
            IConsensusLoop consensusLoop,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            AssemblerOptions options = null)
            : base(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network)
        {
            options = options ?? new AssemblerOptions();
            this.blockMinFeeRate = options.BlockMinFeeRate;

            // Limit weight to between 4K and MAX_BLOCK_WEIGHT-4K for sanity.
            this.blockMaxWeight = (uint)Math.Max(4000, Math.Min(PowMining.DefaultBlockMaxWeight - 4000, options.BlockMaxWeight));

            // Limit size to between 1K and MAX_BLOCK_SERIALIZED_SIZE-1K for sanity.
            this.blockMaxSize = (uint)Math.Max(1000, Math.Min(network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize - 1000, options.BlockMaxSize));

            // Whether we need to account for byte usage (in addition to weight usage).
            this.needSizeAccounting = (this.blockMaxSize < network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize - 1000);

            this.Options = options;

            this.Configure();
        }

        private int ComputeBlockVersion(ChainedBlock prevChainedBlock, NBitcoin.Consensus consensus)
        {
            uint version = ThresholdConditionCache.VersionbitsTopBits;
            var thresholdConditionCache = new ThresholdConditionCache(consensus);

            IEnumerable<BIP9Deployments> deployments = Enum.GetValues(typeof(BIP9Deployments)).OfType<BIP9Deployments>();

            foreach (BIP9Deployments deployment in deployments)
            {
                ThresholdState state = thresholdConditionCache.GetState(prevChainedBlock, deployment);
                if ((state == ThresholdState.LockedIn) || (state == ThresholdState.Started))
                    version |= thresholdConditionCache.Mask(deployment);
            }

            return (int)version;
        }

        /// <inheritdoc/>
        public override BlockTemplate Build(ChainedBlock chainTip, Script scriptPubKey)
        {
            this.Logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(chainTip), chainTip, nameof(scriptPubKey), nameof(scriptPubKey.Length), scriptPubKey.Length);

            this.Configure();

            this.ChainTip = chainTip;

            this.block = this.blockTemplate.Block;
            this.scriptPubKey = scriptPubKey;

            this.CreateCoinbase();
            this.ComputeBlockVersion();

            // TODO: MineBlocksOnDemand
            // -regtest only: allow overriding block.nVersion with
            // -blockversion=N to test forking scenarios
            //if (this.network. chainparams.MineBlocksOnDemand())
            //    pblock->nVersion = GetArg("-blockversion", pblock->nVersion);

            medianTimePast = Utils.DateTimeToUnixTime(this.ChainTip.GetMedianTimePast());
            this.lockTimeCutoff = PowConsensusValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast)
                ? medianTimePast
                : this.block.Header.Time;

            // TODO: Implement Witness Code
            // Decide whether to include witness transactions
            // This is only needed in case the witness softfork activation is reverted
            // (which would require a very deep reorganization) or when
            // -promiscuousmempoolflags is used.
            // TODO: replace this with a call to main to assess validity of a mempool
            // transaction (which in most cases can be a no-op).
            this.includeWitness = false; //IsWitnessEnabled(pindexPrev, chainparams.GetConsensus()) && fMineWitnessTx;

            // add transactions from the mempool
            int nPackagesSelected;
            int nDescendantsUpdated;
            this.AddTransactions(out nPackagesSelected, out nDescendantsUpdated);

            lastBlockTx = this.blockTx;
            lastBlockSize = this.blockSize;
            lastBlockWeight = this.blockWeight;

            // TODO: Implement Witness Code
            // pblocktemplate->CoinbaseCommitment = GenerateCoinbaseCommitment(*pblock, pindexPrev, chainparams.GetConsensus());
            this.blockTemplate.VTxFees[0] = -this.fees;
            this.coinbase.Outputs[0].Value = this.fees + this.ConsensusLoop.Validator.GetProofOfWorkReward(this.height);
            this.blockTemplate.TotalFee = this.fees;

            int nSerializeSize = this.block.GetSerializedSize();
            this.Logger.LogDebug("Serialized size is {0} bytes, block weight is {1}, number of txs is {2}, tx fees are {3}, number of sigops is {4}.", nSerializeSize, this.ConsensusLoop.Validator.GetBlockWeight(this.block), this.blockTx, this.fees, this.blockSigOpsCost);

            this.UpdateHeaders();

            //pblocktemplate->TxSigOpsCost[0] = WITNESS_SCALE_FACTOR * GetLegacySigOpCount(*pblock->vtx[0]);

            this.TestBlockValidity();

            //int64_t nTime2 = GetTimeMicros();

            //LogPrint(BCLog::BENCH, "CreateNewBlock() packages: %.2fms (%d packages, %d updated descendants), validity: %.2fms (total %.2fms)\n", 0.001 * (nTime1 - nTimeStart), nPackagesSelected, nDescendantsUpdated, 0.001 * (nTime2 - nTime1), 0.001 * (nTime2 - nTimeStart));

            this.Logger.LogTrace("(-)");
            return this.blockTemplate;
        }

        /// <summary>
        /// Configures (resets) the builder to its default state 
        /// before constructing a new block.
        /// </summary>
        public void Configure()
        {
            this.blockSize = 1000;
            this.blockTemplate = new BlockTemplate { Block = new Block(), VTxFees = new List<Money>() };
            this.blockTx = 0;
            this.blockWeight = 4000;
            this.blockSigOpsCost = 400;
            this.fees = 0;
            this.inBlock = new TxMempool.SetEntries();
            this.includeWitness = false;
        }

        /// <summary>
        //// Compute the block version.
        /// </summary>
        protected virtual void ComputeBlockVersion()
        {
            this.height = this.ChainTip.Height + 1;
            this.block.Header.Version = this.ComputeBlockVersion(this.ChainTip, this.Network.Consensus);
        }

        /// <summary>
        /// Create coinbase transaction.
        /// Set the coin base with zero money.
        /// Once we have the fee we can update the amount.
        /// </summary>
        protected virtual void CreateCoinbase()
        {
            this.coinbase = new Transaction();
            this.coinbase.Time = (uint)this.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            this.coinbase.AddInput(TxIn.CreateCoinbase(this.ChainTip.Height + 1));
            this.coinbase.AddOutput(new TxOut(Money.Zero, this.scriptPubKey));

            this.block.AddTransaction(this.coinbase);
            this.blockTemplate.VTxFees.Add(-1); // Updated at end.
            this.blockTemplate.TxSigOpsCost.Add(-1); // Updated at end.
        }

        protected virtual void UpdateHeaders()
        {
            this.Logger.LogTrace("()");

            this.block.Header.HashPrevBlock = this.ChainTip.HashBlock;
            this.block.Header.UpdateTime(this.DateTimeProvider.GetTimeOffset(), this.Network, this.ChainTip);
            this.block.Header.Bits = this.block.Header.GetWorkRequired(this.Network, this.ChainTip);
            this.block.Header.Nonce = 0;

            this.Logger.LogTrace("(-)");
        }

        protected virtual void TestBlockValidity()
        {
            this.Logger.LogTrace("()");

            var context = new RuleContext(new BlockValidationContext { Block = this.block }, this.Network.Consensus, this.ConsensusLoop.Tip)
            {
                CheckPow = false,
                CheckMerkleRoot = false,
            };

            this.ConsensusLoop.ValidateBlock(context);

            this.Logger.LogTrace("(-)");
        }

        // Add a tx to the block.
        private void AddToBlock(TxMempoolEntry iter)
        {
            this.Logger.LogTrace("({0}.{1}:'{2}')", nameof(iter), nameof(iter.TransactionHash), iter.TransactionHash);

            this.block.AddTransaction(iter.Transaction);

            this.blockTemplate.VTxFees.Add(iter.Fee);
            this.blockTemplate.TxSigOpsCost.Add(iter.SigOpCost);

            if (this.needSizeAccounting)
                this.blockSize += iter.Transaction.GetSerializedSize();

            this.blockWeight += iter.TxWeight;
            this.blockTx++;
            this.blockSigOpsCost += iter.SigOpCost;
            this.fees += iter.Fee;
            this.inBlock.Add(iter);

            //bool fPrintPriority = GetBoolArg("-printpriority", DEFAULT_PRINTPRIORITY);
            //if (fPrintPriority)
            //{
            // LogPrintf("fee %s txid %s\n",
            //  CFeeRate(iter->GetModifiedFee(), iter->GetTxSize()).ToString(),
            //  iter->GetTx().GetHash().ToString());

            //}
            this.Logger.LogTrace("(-)");
        }

        // Methods for how to add transactions to a block.
        // Add transactions based on feerate including unconfirmed ancestors
        // Increments nPackagesSelected / nDescendantsUpdated with corresponding
        // statistics from the package selection (for logging statistics).
        // This transaction selection algorithm orders the mempool based
        // on feerate of a transaction including all unconfirmed ancestors.
        // Since we don't remove transactions from the mempool as we select them
        // for block inclusion, we need an alternate method of updating the feerate
        // of a transaction with its not-yet-selected ancestors as we go.
        // This is accomplished by walking the in-mempool descendants of selected
        // transactions and storing a temporary modified state in mapModifiedTxs.
        // Each time through the loop, we compare the best transaction in
        // mapModifiedTxs with the next transaction in the mempool to decide what
        // transaction package to work on next.
        protected virtual void AddTransactions(out int nPackagesSelected, out int nDescendantsUpdated)
        {
            nPackagesSelected = 0;
            nDescendantsUpdated = 0;
            this.Logger.LogTrace("({0}:{1},{2}:{3})", nameof(nPackagesSelected), nPackagesSelected, nameof(nDescendantsUpdated), nDescendantsUpdated);

            // mapModifiedTx will store sorted packages after they are modified
            // because some of their txs are already in the block.
            var mapModifiedTx = new Dictionary<uint256, TxMemPoolModifiedEntry>();

            //var mapModifiedTxRes = this.mempoolScheduler.ReadAsync(() => mempool.MapTx.Values).GetAwaiter().GetResult();
            // mapModifiedTxRes.Select(s => new TxMemPoolModifiedEntry(s)).OrderBy(o => o, new CompareModifiedEntry());

            // Keep track of entries that failed inclusion, to avoid duplicate work.
            TxMempool.SetEntries failedTx = new TxMempool.SetEntries();

            // Start by adding all descendants of previously added txs to mapModifiedTx
            // and modifying them for their already included ancestors.
            this.UpdatePackagesForAdded(this.inBlock, mapModifiedTx);

            List<TxMempoolEntry> ancestorScoreList = this.MempoolLock.ReadAsync(() => this.Mempool.MapTx.AncestorScore).GetAwaiter().GetResult().ToList();

            TxMempoolEntry iter;

            int nConsecutiveFailed = 0;
            while (ancestorScoreList.Any() || mapModifiedTx.Any())
            {
                TxMempoolEntry mi = ancestorScoreList.FirstOrDefault();
                if (mi != null)
                {
                    // Skip entries in mapTx that are already in a block or are present
                    // in mapModifiedTx (which implies that the mapTx ancestor state is
                    // stale due to ancestor inclusion in the block).
                    // Also skip transactions that we've already failed to add. This can happen if
                    // we consider a transaction in mapModifiedTx and it fails: we can then
                    // potentially consider it again while walking mapTx.  It's currently
                    // guaranteed to fail again, but as a belt-and-suspenders check we put it in
                    // failedTx and avoid re-evaluation, since the re-evaluation would be using
                    // cached size/sigops/fee values that are not actually correct.

                    // First try to find a new transaction in mapTx to evaluate.
                    if (mapModifiedTx.ContainsKey(mi.TransactionHash) || this.inBlock.Contains(mi) || failedTx.Contains(mi))
                    {
                        ancestorScoreList.Remove(mi);
                        continue;
                    }
                }

                // Now that mi is not stale, determine which transaction to evaluate:
                // the next entry from mapTx, or the best from mapModifiedTx?
                bool fUsingModified = false;
                TxMemPoolModifiedEntry modit;
                var compare = new CompareModifiedEntry();
                if (mi == null)
                {
                    modit = mapModifiedTx.Values.OrderByDescending(o => o, compare).First();
                    iter = modit.iter;
                    fUsingModified = true;
                }
                else
                {
                    // Try to compare the mapTx entry to the mapModifiedTx entry
                    iter = mi;

                    modit = mapModifiedTx.Values.OrderByDescending(o => o, compare).FirstOrDefault();
                    if ((modit != null) && (compare.Compare(modit, new TxMemPoolModifiedEntry(iter)) > 0))
                    {
                        // The best entry in mapModifiedTx has higher score
                        // than the one from mapTx..
                        // Switch which transaction (package) to consider.

                        iter = modit.iter;
                        fUsingModified = true;
                    }
                    else
                    {
                        // Either no entry in mapModifiedTx, or it's worse than mapTx.
                        // Increment mi for the next loop iteration.
                        ancestorScoreList.Remove(iter);
                    }
                }

                // We skip mapTx entries that are inBlock, and mapModifiedTx shouldn't
                // contain anything that is inBlock.
                Guard.Assert(!this.inBlock.Contains(iter));

                long packageSize = iter.SizeWithAncestors;
                Money packageFees = iter.ModFeesWithAncestors;
                long packageSigOpsCost = iter.SizeWithAncestors;
                if (fUsingModified)
                {
                    packageSize = modit.SizeWithAncestors;
                    packageFees = modit.ModFeesWithAncestors;
                    packageSigOpsCost = modit.SigOpCostWithAncestors;
                }

                if (packageFees < this.blockMinFeeRate.GetFee((int)packageSize))
                {
                    // Everything else we might consider has a lower fee rate
                    return;
                }

                if (!this.TestPackage(packageSize, packageSigOpsCost))
                {
                    if (fUsingModified)
                    {
                        // Since we always look at the best entry in mapModifiedTx,
                        // we must erase failed entries so that we can consider the
                        // next best entry on the next loop iteration
                        mapModifiedTx.Remove(modit.iter.TransactionHash);
                        failedTx.Add(iter);
                    }

                    nConsecutiveFailed++;

                    if ((nConsecutiveFailed > this.MaxConsecutiveAddTransactionFailures) && (this.blockWeight > this.blockMaxWeight - 4000))
                    {
                        // Give up if we're close to full and haven't succeeded in a while
                        break;
                    }
                    continue;
                }

                TxMempool.SetEntries ancestors = new TxMempool.SetEntries();
                long nNoLimit = long.MaxValue;
                string dummy;
                this.Mempool.CalculateMemPoolAncestors(iter, ancestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);

                this.OnlyUnconfirmed(ancestors);
                ancestors.Add(iter);

                // Test if all tx's are Final.
                if (!this.TestPackageTransactions(ancestors))
                {
                    if (fUsingModified)
                    {
                        mapModifiedTx.Remove(modit.iter.TransactionHash);
                        failedTx.Add(iter);
                    }
                    continue;
                }

                // This transaction will make it in; reset the failed counter.
                nConsecutiveFailed = 0;

                // Package can be added. Sort the entries in a valid order.
                // Sort package by ancestor count
                // If a transaction A depends on transaction B, then A's ancestor count
                // must be greater than B's.  So this is sufficient to validly order the
                // transactions for block inclusion.
                List<TxMempoolEntry> sortedEntries = ancestors.ToList().OrderBy(o => o, new CompareTxIterByAncestorCount()).ToList();
                foreach (TxMempoolEntry sortedEntry in sortedEntries)
                {
                    this.AddToBlock(sortedEntry);
                    // Erase from the modified set, if present
                    mapModifiedTx.Remove(sortedEntry.TransactionHash);
                }

                nPackagesSelected++;

                // Update transactions that depend on each of these
                nDescendantsUpdated += this.UpdatePackagesForAdded(ancestors, mapModifiedTx);
            }

            this.Logger.LogTrace("(-)");
        }

        // Remove confirmed (inBlock) entries from given set
        private void OnlyUnconfirmed(TxMempool.SetEntries testSet)
        {
            foreach (var setEntry in testSet.ToList())
            {
                // Only test txs not already in the block
                if (this.inBlock.Contains(setEntry))
                {
                    testSet.Remove(setEntry);
                }
            }
        }

        // Test if a new package would "fit" in the block.
        private bool TestPackage(long packageSize, long packageSigOpsCost)
        {
            // TODO: Switch to weight-based accounting for packages instead of vsize-based accounting.
            if (this.blockWeight + this.Network.Consensus.Option<PowConsensusOptions>().WitnessScaleFactor * packageSize >= this.blockMaxWeight)
                return false;

            if (this.blockSigOpsCost + packageSigOpsCost >= this.Network.Consensus.Option<PowConsensusOptions>().MaxBlockSigopsCost)
                return false;

            return true;
        }

        // Perform transaction-level checks before adding to block:
        // - transaction finality (locktime)
        // - premature witness (in case segwit transactions are added to mempool before
        //   segwit activation)
        // - serialized size (in case -blockmaxsize is in use)
        private bool TestPackageTransactions(TxMempool.SetEntries package)
        {
            long nPotentialBlockSize = this.blockSize; // only used with needSizeAccounting
            foreach (TxMempoolEntry it in package)
            {
                if (!it.Transaction.IsFinal(Utils.UnixTimeToDateTime(this.lockTimeCutoff), this.height))
                    return false;

                if (!this.includeWitness && it.Transaction.HasWitness)
                    return false;

                if (this.needSizeAccounting)
                {
                    int nTxSize = it.Transaction.GetSerializedSize();
                    if (nPotentialBlockSize + nTxSize >= this.blockMaxSize)
                        return false;

                    nPotentialBlockSize += nTxSize;
                }
            }

            return true;
        }

        // Add descendants of given transactions to mapModifiedTx with ancestor
        // state updated assuming given transactions are inBlock. Returns number
        // of updated descendants.
        private int UpdatePackagesForAdded(TxMempool.SetEntries alreadyAdded, Dictionary<uint256, TxMemPoolModifiedEntry> mapModifiedTx)
        {
            int descendantsUpdated = 0;
            foreach (TxMempoolEntry setEntry in alreadyAdded)
            {
                TxMempool.SetEntries setEntries = new TxMempool.SetEntries();
                this.MempoolLock.ReadAsync(() => this.Mempool.CalculateDescendants(setEntry, setEntries)).GetAwaiter().GetResult();
                foreach (var desc in setEntries)
                {
                    if (alreadyAdded.Contains(desc))
                        continue;

                    descendantsUpdated++;
                    TxMemPoolModifiedEntry modEntry;
                    if (!mapModifiedTx.TryGetValue(desc.TransactionHash, out modEntry))
                    {
                        modEntry = new TxMemPoolModifiedEntry(desc);
                        mapModifiedTx.Add(desc.TransactionHash, modEntry);
                    }
                    modEntry.SizeWithAncestors -= setEntry.GetTxSize();
                    modEntry.ModFeesWithAncestors -= setEntry.ModifiedFee;
                    modEntry.SigOpCostWithAncestors -= setEntry.SigOpCost;
                }
            }

            return descendantsUpdated;
        }
    }
}