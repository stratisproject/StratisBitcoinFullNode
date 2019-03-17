using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner.Comparers;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// A high level class that will allow the ability to override or inject functionality based on what type of block creation logic is used.
    /// </summary>
    public abstract class BlockDefinition
    {
        /// <summary>
        /// Tip of the chain that this instance will work with without touching any shared chain resources.
        /// </summary>
        protected ChainedHeader ChainTip;

        /// <summary>Manager of the longest fully validated chain of blocks.</summary>
        protected readonly IConsensusManager ConsensusManager;

        /// <summary>Provider of date time functions.</summary>
        protected readonly IDateTimeProvider DateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        protected readonly ITxMempool Mempool;

        /// <summary>Lock for memory pool access.</summary>
        protected readonly MempoolSchedulerLock MempoolLock;

        /// <summary>The current network.</summary>
        protected readonly Network Network;

        /// <summary>Assembler options specific to the assembler e.g. <see cref="BlockDefinitionOptions.BlockMaxSize"/>.</summary>
        protected BlockDefinitionOptions Options;

        /// <summary>
        /// Limit the number of attempts to add transactions to the block when it is
        /// close to full; this is just a simple heuristic to finish quickly if the
        /// mempool has a lot of entries.
        /// </summary>
        protected const int MaxConsecutiveAddTransactionFailures = 1000;

        /// <summary>
        /// Unconfirmed transactions in the memory pool often depend on other
        /// transactions in the memory pool. When we select transactions from the
        /// pool, we select by highest fee rate of a transaction combined with all
        /// its ancestors.
        /// </summary>
        protected long LastBlockTx = 0;

        protected long LastBlockSize = 0;

        protected long LastBlockWeight = 0;

        protected long MedianTimePast;

        /// <summary>
        /// The constructed block template.
        /// </summary>
        protected BlockTemplate BlockTemplate;

        /// <summary>
        /// A convenience pointer that always refers to the <see cref="Block"/> in <see cref="BlockTemplate"/>.
        /// </summary>
        protected Block block;

        /// <summary>
        /// Configuration parameters for the block size.
        /// </summary>
        protected bool IncludeWitness;

        protected bool NeedSizeAccounting;

        protected FeeRate BlockMinFeeRate;

        /// <summary>
        /// Information on the current status of the block.
        /// </summary>
        protected long BlockWeight;

        protected long BlockSize;

        protected long BlockTx;

        protected long BlockSigOpsCost;

        public Money fees;

        protected TxMempool.SetEntries inBlock;

        protected Transaction coinbase;

        /// <summary>
        /// Chain context for the block.
        /// </summary>
        protected int height;

        protected long LockTimeCutoff;

        protected Script scriptPubKey;

        protected BlockDefinition(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            MinerSettings minerSettings,
            Network network)
        {
            this.ConsensusManager = consensusManager;
            this.DateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.Mempool = mempool;
            this.MempoolLock = mempoolLock;
            this.Network = network;

            this.Options = minerSettings.BlockDefinitionOptions;
            this.BlockMinFeeRate = this.Options.BlockMinFeeRate;

            // Whether we need to account for byte usage (in addition to weight usage).
            this.NeedSizeAccounting = (this.Options.BlockMaxSize < network.Consensus.Options.MaxBlockSerializedSize);

            this.Configure();
        }

        /// <summary>
        /// Compute the block version.
        /// </summary>
        protected virtual void ComputeBlockVersion()
        {
            this.height = this.ChainTip.Height + 1;
            var headerVersionRule = this.ConsensusManager.ConsensusRules.GetRule<HeaderVersionRule>();
            this.block.Header.Version = headerVersionRule.ComputeBlockVersion(this.ChainTip);
        }

        /// <summary>
        /// Create coinbase transaction.
        /// Set the coin base with zero money.
        /// Once we have the fee we can update the amount.
        /// </summary>
        protected virtual void CreateCoinbase()
        {
            this.coinbase = this.Network.CreateTransaction();
            this.coinbase.Time = (uint)this.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            this.coinbase.AddInput(TxIn.CreateCoinbase(this.ChainTip.Height + 1));
            this.coinbase.AddOutput(new TxOut(Money.Zero, this.scriptPubKey));

            this.block.AddTransaction(this.coinbase);
        }

        /// <summary>
        /// Configures (resets) the builder to its default state
        /// before constructing a new block.
        /// </summary>
        private void Configure()
        {
            this.BlockSize = 1000;
            this.BlockTemplate = new BlockTemplate(this.Network);
            this.BlockTx = 0;
            this.BlockWeight = 1000 * this.Network.Consensus.Options.WitnessScaleFactor;
            this.BlockSigOpsCost = 400;
            this.fees = 0;
            this.inBlock = new TxMempool.SetEntries();
            this.IncludeWitness = false;
        }

        /// <summary>
        /// Constructs a block template which will be passed to consensus.
        /// </summary>
        /// <param name="chainTip">Tip of the chain that this instance will work with without touching any shared chain resources.</param>
        /// <param name="scriptPubKey">Script that explains what conditions must be met to claim ownership of a coin.</param>
        /// <returns>The contructed <see cref="Mining.BlockTemplate"/>.</returns>
        protected void OnBuild(ChainedHeader chainTip, Script scriptPubKey)
        {
            this.Configure();

            this.ChainTip = chainTip;

            this.block = this.BlockTemplate.Block;
            this.scriptPubKey = scriptPubKey;

            this.CreateCoinbase();
            this.ComputeBlockVersion();

            // TODO: MineBlocksOnDemand
            // -regtest only: allow overriding block.nVersion with
            // -blockversion=N to test forking scenarios
            //if (this.network. chainparams.MineBlocksOnDemand())
            //    pblock->nVersion = GetArg("-blockversion", pblock->nVersion);

            this.MedianTimePast = Utils.DateTimeToUnixTime(this.ChainTip.GetMedianTimePast());
            this.LockTimeCutoff = MempoolValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast)
                ? this.MedianTimePast
                : this.block.Header.Time;

            // TODO: Implement Witness Code
            // Decide whether to include witness transactions
            // This is only needed in case the witness softfork activation is reverted
            // (which would require a very deep reorganization) or when
            // -promiscuousmempoolflags is used.
            // TODO: replace this with a call to main to assess validity of a mempool
            // transaction (which in most cases can be a no-op).
            this.IncludeWitness = false; //IsWitnessEnabled(pindexPrev, chainparams.GetConsensus()) && fMineWitnessTx;

            // add transactions from the mempool
            int nPackagesSelected;
            int nDescendantsUpdated;
            this.AddTransactions(out nPackagesSelected, out nDescendantsUpdated);

            this.LastBlockTx = this.BlockTx;
            this.LastBlockSize = this.BlockSize;
            this.LastBlockWeight = this.BlockWeight;

            // TODO: Implement Witness Code
            // pblocktemplate->CoinbaseCommitment = GenerateCoinbaseCommitment(*pblock, pindexPrev, chainparams.GetConsensus());

            var coinviewRule = this.ConsensusManager.ConsensusRules.GetRule<CoinViewRule>();
            this.coinbase.Outputs[0].Value = this.fees + coinviewRule.GetProofOfWorkReward(this.height);
            this.BlockTemplate.TotalFee = this.fees;

            int nSerializeSize = this.block.GetSerializedSize();
            this.logger.LogDebug("Serialized size is {0} bytes, block weight is {1}, number of txs is {2}, tx fees are {3}, number of sigops is {4}.", nSerializeSize, this.block.GetBlockWeight(this.Network.Consensus), this.BlockTx, this.fees, this.BlockSigOpsCost);

            this.UpdateHeaders();
        }

        /// <summary>
        /// Network specific logic to add a transaction to the block from a given mempool entry.
        /// </summary>
        public abstract void AddToBlock(TxMempoolEntry mempoolEntry);

        /// <summary>
        /// Adds a transaction to the block and updates the <see cref="BlockSize"/> and <see cref="BlockTx"/> values.
        /// </summary>
        protected void AddTransactionToBlock(Transaction transaction)
        {
            this.block.AddTransaction(transaction);
            this.BlockTx++;

            if (this.NeedSizeAccounting)
                this.BlockSize += transaction.GetSerializedSize();
        }

        /// <summary>
        /// Updates block statistics from the given mempool entry.
        /// <para>The block's <see cref="BlockSigOpsCost"/> and <see cref="BlockWeight"/> values are adjusted.
        /// </para>
        /// </summary>
        protected void UpdateBlockStatistics(TxMempoolEntry mempoolEntry)
        {
            this.BlockSigOpsCost += mempoolEntry.SigOpCost;
            this.BlockWeight += mempoolEntry.TxWeight;
            this.inBlock.Add(mempoolEntry);
        }

        /// <summary>
        /// Updates the total fee amount for this block.
        /// </summary>
        protected void UpdateTotalFees(Money fee)
        {
            this.fees += fee;
        }

        /// <summary>
        /// Method for how to add transactions to a block.
        /// Add transactions based on feerate including unconfirmed ancestors
        /// Increments nPackagesSelected / nDescendantsUpdated with corresponding
        /// statistics from the package selection (for logging statistics).
        /// This transaction selection algorithm orders the mempool based
        /// on feerate of a transaction including all unconfirmed ancestors.
        /// Since we don't remove transactions from the mempool as we select them
        /// for block inclusion, we need an alternate method of updating the feerate
        /// of a transaction with its not-yet-selected ancestors as we go.
        /// This is accomplished by walking the in-mempool descendants of selected
        /// transactions and storing a temporary modified state in mapModifiedTxs.
        /// Each time through the loop, we compare the best transaction in
        /// mapModifiedTxs with the next transaction in the mempool to decide what
        /// transaction package to work on next.
        /// </summary>
        protected virtual void AddTransactions(out int nPackagesSelected, out int nDescendantsUpdated)
        {
            nPackagesSelected = 0;
            nDescendantsUpdated = 0;

            // mapModifiedTx will store sorted packages after they are modified
            // because some of their txs are already in the block.
            var mapModifiedTx = new Dictionary<uint256, TxMemPoolModifiedEntry>();

            //var mapModifiedTxRes = this.mempoolScheduler.ReadAsync(() => mempool.MapTx.Values).GetAwaiter().GetResult();
            // mapModifiedTxRes.Select(s => new TxMemPoolModifiedEntry(s)).OrderBy(o => o, new CompareModifiedEntry());

            // Keep track of entries that failed inclusion, to avoid duplicate work.
            var failedTx = new TxMempool.SetEntries();

            // Start by adding all descendants of previously added txs to mapModifiedTx
            // and modifying them for their already included ancestors.
            this.UpdatePackagesForAdded(this.inBlock, mapModifiedTx);

            List<TxMempoolEntry> ancestorScoreList = this.MempoolLock.ReadAsync(() => this.Mempool.MapTx.AncestorScore).ConfigureAwait(false).GetAwaiter().GetResult().ToList();

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
                    modit = mapModifiedTx.Values.OrderBy(o => o, compare).First();
                    iter = modit.MempoolEntry;
                    fUsingModified = true;
                }
                else
                {
                    // Try to compare the mapTx entry to the mapModifiedTx entry
                    iter = mi;

                    modit = mapModifiedTx.Values.OrderBy(o => o, compare).FirstOrDefault();
                    if ((modit != null) && (compare.Compare(modit, new TxMemPoolModifiedEntry(iter)) < 0))
                    {
                        // The best entry in mapModifiedTx has higher score
                        // than the one from mapTx..
                        // Switch which transaction (package) to consider.

                        iter = modit.MempoolEntry;
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
                long packageSigOpsCost = iter.SigOpCostWithAncestors;
                if (fUsingModified)
                {
                    packageSize = modit.SizeWithAncestors;
                    packageFees = modit.ModFeesWithAncestors;
                    packageSigOpsCost = modit.SigOpCostWithAncestors;
                }

                if (packageFees < this.BlockMinFeeRate.GetFee((int)packageSize))
                {
                    // Everything else we might consider has a lower fee rate
                    return;
                }

                if (!this.TestPackage(iter, packageSize, packageSigOpsCost))
                {
                    if (fUsingModified)
                    {
                        // Since we always look at the best entry in mapModifiedTx,
                        // we must erase failed entries so that we can consider the
                        // next best entry on the next loop iteration
                        mapModifiedTx.Remove(modit.MempoolEntry.TransactionHash);
                        failedTx.Add(iter);
                    }

                    nConsecutiveFailed++;

                    if ((nConsecutiveFailed > MaxConsecutiveAddTransactionFailures) && (this.BlockWeight > this.Options.BlockMaxWeight - 4000))
                    {
                        // Give up if we're close to full and haven't succeeded in a while
                        break;
                    }
                    continue;
                }

                var ancestors = new TxMempool.SetEntries();
                long nNoLimit = long.MaxValue;
                string dummy;

                this.MempoolLock.ReadAsync(() => this.Mempool.CalculateMemPoolAncestors(iter, ancestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false)).ConfigureAwait(false).GetAwaiter().GetResult();

                this.OnlyUnconfirmed(ancestors);
                ancestors.Add(iter);

                // Test if all tx's are Final.
                if (!this.TestPackageTransactions(ancestors))
                {
                    if (fUsingModified)
                    {
                        mapModifiedTx.Remove(modit.MempoolEntry.TransactionHash);
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
        }

        /// <summary>
        /// Remove confirmed <see cref="inBlock"/> entries from given set.
        /// </summary>
        private void OnlyUnconfirmed(TxMempool.SetEntries testSet)
        {
            foreach (TxMempoolEntry setEntry in testSet.ToList())
            {
                // Only test txs not already in the block
                if (this.inBlock.Contains(setEntry))
                {
                    testSet.Remove(setEntry);
                }
            }
        }

        /// <summary>
        /// Test if a new package would "fit" in the block.
        /// </summary>
        protected virtual bool TestPackage(TxMempoolEntry entry, long packageSize, long packageSigOpsCost)
        {
            // TODO: Switch to weight-based accounting for packages instead of vsize-based accounting.
            if (this.BlockWeight + this.Network.Consensus.Options.WitnessScaleFactor * packageSize >= this.Options.BlockMaxWeight)
            {
                this.logger.LogTrace("(-)[MAX_WEIGHT_REACHED]:false");
                return false;
            }

            if (this.BlockSigOpsCost + packageSigOpsCost >= this.Network.Consensus.Options.MaxBlockSigopsCost)
            {
                this.logger.LogTrace("(-)[MAX_SIGOPS_REACHED]:false");
                return false;
            }

            this.logger.LogTrace("(-):true");
            return true;
        }

        /// <summary>
        /// Perform transaction-level checks before adding to block.
        /// <para>
        /// <list>
        /// <item>Transaction finality (locktime).</item>
        /// <item>Premature witness (in case segwit transactions are added to mempool before segwit activation).</item>
        /// <item>serialized size (in case -blockmaxsize is in use).</item>
        /// </list>
        /// </para>
        /// </summary>
        private bool TestPackageTransactions(TxMempool.SetEntries package)
        {
            foreach (TxMempoolEntry it in package)
            {
                if (!it.Transaction.IsFinal(Utils.UnixTimeToDateTime(this.LockTimeCutoff), this.height))
                    return false;

                if (!this.IncludeWitness && it.Transaction.HasWitness)
                    return false;

                if (this.NeedSizeAccounting)
                {
                    long nPotentialBlockSize = this.BlockSize; // only used with needSizeAccounting
                    int nTxSize = it.Transaction.GetSerializedSize();
                    if (nPotentialBlockSize + nTxSize >= this.Options.BlockMaxSize)
                        return false;

                    nPotentialBlockSize += nTxSize;
                }
            }

            return true;
        }

        /// <summary>
        /// Add descendants of given transactions to mapModifiedTx with ancestor
        /// state updated assuming given transactions are inBlock. Returns number
        /// of updated descendants.
        /// </summary>
        private int UpdatePackagesForAdded(TxMempool.SetEntries alreadyAdded, Dictionary<uint256, TxMemPoolModifiedEntry> mapModifiedTx)
        {
            int descendantsUpdated = 0;

            foreach (TxMempoolEntry addedEntry in alreadyAdded)
            {
                var setEntries = new TxMempool.SetEntries();

                this.MempoolLock.ReadAsync(() =>
                {
                    if (!this.Mempool.MapTx.ContainsKey(addedEntry.TransactionHash))
                    {
                        this.logger.LogWarning("{0} is not present in {1} any longer, skipping.", addedEntry.TransactionHash, nameof(this.Mempool.MapTx));
                        return;
                    }

                    this.Mempool.CalculateDescendants(addedEntry, setEntries);

                }).GetAwaiter().GetResult();

                foreach (TxMempoolEntry desc in setEntries)
                {
                    if (alreadyAdded.Contains(desc))
                        continue;

                    descendantsUpdated++;
                    TxMemPoolModifiedEntry modEntry;
                    if (!mapModifiedTx.TryGetValue(desc.TransactionHash, out modEntry))
                    {
                        modEntry = new TxMemPoolModifiedEntry(desc);
                        mapModifiedTx.Add(desc.TransactionHash, modEntry);
                        this.logger.LogDebug("Added transaction '{0}' to the block template because it's a required ancestor for '{1}'.", desc.TransactionHash, addedEntry.TransactionHash);
                    }

                    modEntry.SizeWithAncestors -= addedEntry.GetTxSize();
                    modEntry.ModFeesWithAncestors -= addedEntry.ModifiedFee;
                    modEntry.SigOpCostWithAncestors -= addedEntry.SigOpCost;
                }
            }

            return descendantsUpdated;
        }

        /// <summary>Network specific logic specific as to how the block will be built.</summary>
        public abstract BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey);

        /// <summary>Update the block's header information.</summary>
        protected void UpdateBaseHeaders()
        {
            this.block.Header.HashPrevBlock = this.ChainTip.HashBlock;
            this.block.Header.UpdateTime(this.DateTimeProvider.GetTimeOffset(), this.Network, this.ChainTip);
            this.block.Header.Nonce = 0;
        }

        /// <summary>Network specific logic specific as to how the block's header will be set.</summary>
        public abstract void UpdateHeaders();
    }
}