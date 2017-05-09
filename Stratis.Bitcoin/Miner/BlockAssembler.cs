using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Miner
{
	public class BlockTemplate
	{
		public BlockTemplate()
		{
			block = new Block();
			vTxFees = new List<Money>();
			vTxSigOpsCost = new List<long>();
		}

		public Block block;
		public List<Money> vTxFees;
		public List<long> vTxSigOpsCost;
		public string vchCoinbaseCommitment;
	};

	public class BlockAssembler
    {
		// Unconfirmed transactions in the memory pool often depend on other
		// transactions in the memory pool. When we select transactions from the
		// pool, we select by highest fee rate of a transaction combined with all
		// its ancestors.

		static long nLastBlockTx = 0;
		static long nLastBlockSize = 0;
		static long nLastBlockWeight = 0;

		private readonly ConcurrentChain chain;
	    private readonly MempoolScheduler mempoolScheduler;
	    private readonly TxMempool mempool;
	    private readonly IDateTimeProvider dateTimeProvider;
	    private readonly ConsensusOptions consensusOptions;
	    private readonly Options options;
	    // The constructed block template
	    private BlockTemplate pblocktemplate;
		// A convenience pointer that always refers to the CBlock in pblocktemplate
		private Block pblock;

		// Configuration parameters for the block size
		private bool fIncludeWitness;
		private uint nBlockMaxWeight, nBlockMaxSize;
		private bool fNeedSizeAccounting;
		private FeeRate blockMinFeeRate;

		// Information on the current status of the block
		private long nBlockWeight;
		private long nBlockSize;
		private long nBlockTx;
		private long nBlockSigOpsCost;
		private Money nFees;
		private TxMempool.SetEntries inBlock;

		// Chain context for the block
		private int nHeight;
		private long nLockTimeCutoff;
		private Network network;


	    public class Options
	    {

		    public long nBlockMaxWeight;
		    public long nBlockMaxSize;
		    public FeeRate blockMinFeeRate;
	    };

	    public BlockAssembler(Network network, ConcurrentChain chain, MempoolScheduler mempoolScheduler, TxMempool mempool, 
			IDateTimeProvider dateTimeProvider, ConsensusOptions consensusOptions, Options options = null)
		{
			this.chain = chain;
			this.mempoolScheduler = mempoolScheduler;
			this.mempool = mempool;
			this.dateTimeProvider = dateTimeProvider;
			this.consensusOptions = consensusOptions;
			this.options = options;
			this.network = network;

			inBlock = new TxMempool.SetEntries();

			// Reserve space for coinbase tx
			nBlockSize = 1000;
			nBlockWeight = 4000;
			nBlockSigOpsCost = 400;
			fIncludeWitness = false;

			// These counters do not include coinbase tx
			nBlockTx = 0;
			nFees = 0;

			pblocktemplate = new BlockTemplate {block = new Block(), vTxFees = new List<Money>()};
		}

	    private int ComputeBlockVersion(ChainedBlock pindexPrev, NBitcoin.Consensus consensus)
	    {
		    var nVersion = ThresholdConditionCache.VERSIONBITS_TOP_BITS;
		    var thresholdConditionCache = new ThresholdConditionCache(consensus);

		    var deploymensts = Enum.GetValues(typeof(BIP9Deployments))
			    .OfType<BIP9Deployments>();

		    foreach (var deployment in deploymensts)
		    {
			    var state = thresholdConditionCache.GetState(pindexPrev, deployment);
			    if (state == ThresholdState.LockedIn || state == ThresholdState.Started)
			    {
				    nVersion |= thresholdConditionCache.Mask(deployment);
			    }
		    }

		    return (int)nVersion;
	    }

	    private static long nMedianTimePast;
		const long TicksPerMicrosecond = 10;
		/** Construct a new block template with coinbase to scriptPubKeyIn */
		public BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
		{
			long nTimeStart = DateTime.UtcNow.Ticks/TicksPerMicrosecond;
			pblock = pblocktemplate.block; // pointer for convenience

			// Create coinbase transaction.
			// set the coin base with zer money 
			// once we have the fee we can update the amount
			var coinbase = new Transaction();
			coinbase.AddInput(TxIn.CreateCoinbase(this.chain.Height + 1));
			coinbase.AddOutput(new TxOut(Money.Zero, scriptPubKeyIn)); 
			pblock.AddTransaction(coinbase);
			pblocktemplate.vTxFees.Add(-1); // updated at end
			pblocktemplate.vTxSigOpsCost.Add(-1); // updated at end

			// compute the block version
			var pindexPrev = this.chain.Tip;
			nHeight = pindexPrev.Height + 1;
			pblock.Header.Version = ComputeBlockVersion(pindexPrev, this.network.Consensus);

			// TODO: MineBlocksOnDemand
			// -regtest only: allow overriding block.nVersion with
			// -blockversion=N to test forking scenarios
			//if (this.network. chainparams.MineBlocksOnDemand())
			//	pblock->nVersion = GetArg("-blockversion", pblock->nVersion);

			nMedianTimePast = pindexPrev.GetMedianTimePast().Ticks;
			nLockTimeCutoff = true //(STANDARD_LOCKTIME_VERIFY_FLAGS & LOCKTIME_MEDIAN_TIME_PAST)
				? nMedianTimePast
				: pblock.Header.Time;

			// TODO: Implement Witness Code
			// Decide whether to include witness transactions
			// This is only needed in case the witness softfork activation is reverted
			// (which would require a very deep reorganization) or when
			// -promiscuousmempoolflags is used.
			// TODO: replace this with a call to main to assess validity of a mempool
			// transaction (which in most cases can be a no-op).
			fIncludeWitness = false; //IsWitnessEnabled(pindexPrev, chainparams.GetConsensus()) && fMineWitnessTx;

			// add transactions from the mempool
			int nPackagesSelected = 0;
			int nDescendantsUpdated = 0;
			AddTransactions(nPackagesSelected, nDescendantsUpdated);

			long nTime1 = DateTime.UtcNow.Ticks / TicksPerMicrosecond;
			nLastBlockTx = nBlockTx;
			nLastBlockSize = nBlockSize;
			nLastBlockWeight = nBlockWeight;

			// TODO: Implement Witness Code
			// pblocktemplate->vchCoinbaseCommitment = GenerateCoinbaseCommitment(*pblock, pindexPrev, chainparams.GetConsensus());
			pblocktemplate.vTxFees[0] = -nFees;

			var nSerializeSize = pblock.GetSerializedSize();
			Logs.Mining.LogInformation("CreateNewBlock()");
			//LogPrintf("CreateNewBlock(): total size: %u block weight: %u txs: %u fees: %ld sigops %d\n", nSerializeSize, GetBlockWeight(*pblock), nBlockTx, nFees, nBlockSigOpsCost);

			// Fill in header
			pblock.Header.HashPrevBlock = pindexPrev.HashBlock;
			pblock.Header.UpdateTime(dateTimeProvider.GetTimeOffset(), this.network, this.chain.Tip);
			pblock.Header.Bits = pblock.Header.GetWorkRequired(this.network, this.chain.Tip);
			pblock.Header.Nonce = 0;

			//pblocktemplate->vTxSigOpsCost[0] = WITNESS_SCALE_FACTOR * GetLegacySigOpCount(*pblock->vtx[0]);

			// TODO : find a way to inject the consensus code to validate a block
			//CValidationState state;
			//if (!TestBlockValidity(state, chainparams, *pblock, pindexPrev, false, false))
			//{
			//	throw std::runtime_error(strprintf("%s: TestBlockValidity failed: %s", __func__, FormatStateMessage(state)));
			//}
			//int64_t nTime2 = GetTimeMicros();

			//LogPrint(BCLog::BENCH, "CreateNewBlock() packages: %.2fms (%d packages, %d updated descendants), validity: %.2fms (total %.2fms)\n", 0.001 * (nTime1 - nTimeStart), nPackagesSelected, nDescendantsUpdated, 0.001 * (nTime2 - nTime1), 0.001 * (nTime2 - nTimeStart));

			return pblocktemplate;
		}

		// Add a tx to the block 
	    private void AddToBlock(TxMempoolEntry iter)
	    {
		    pblock.AddTransaction(iter.Transaction);

		    pblocktemplate.vTxFees.Add(iter.Fee);
		    pblocktemplate.vTxSigOpsCost.Add(iter.SigOpCost);
		    if (fNeedSizeAccounting)
		    {
			    nBlockSize += iter.Transaction.GetSerializedSize();
		    }
		    nBlockWeight += iter.TxWeight;
		    ++nBlockTx;
		    nBlockSigOpsCost += iter.SigOpCost;
		    nFees += iter.Fee;
		    inBlock.Add(iter);

		    //bool fPrintPriority = GetBoolArg("-printpriority", DEFAULT_PRINTPRIORITY);
		    //if (fPrintPriority)
		    //{
			   // LogPrintf("fee %s txid %s\n",
				  //  CFeeRate(iter->GetModifiedFee(), iter->GetTxSize()).ToString(),
				  //  iter->GetTx().GetHash().ToString());

		    //}
	    }

	    // Container for tracking updates to ancestor feerate as we include (parent)
		// transactions in a block
		public class TxMemPoolModifiedEntry
		{
			public TxMemPoolModifiedEntry(TxMempoolEntry entry)
			{
				iter = entry;
				SizeWithAncestors = entry.SizeWithAncestors;
				ModFeesWithAncestors = entry.ModFeesWithAncestors;
				SigOpCostWithAncestors = entry.SigOpCostWithAncestors;
			}

			public TxMempoolEntry iter;
			public long SizeWithAncestors;
			public Money ModFeesWithAncestors;
			public long SigOpCostWithAncestors;
		};

		// This matches the calculation in CompareTxMemPoolEntryByAncestorFee,
		// except operating on CTxMemPoolModifiedEntry.
		// TODO: refactor to avoid duplication of this logic.
	    public class CompareModifiedEntry : IComparer<TxMemPoolModifiedEntry>
	    {
		    public int Compare(TxMemPoolModifiedEntry a, TxMemPoolModifiedEntry b)
		    {
			    var f1 = a.ModFeesWithAncestors*b.SizeWithAncestors;
			    var f2 = b.ModFeesWithAncestors*a.SizeWithAncestors;
			    if (f1 == f2)
				    return TxMempool.CompareIteratorByHash.InnerCompare(a.iter, b.iter);
			    if (f1 > f2)
				    return 1;
			    return -1;
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
				    if (a.CountWithAncestors < b.CountWithAncestors)
					    return -1;
				    else
					    return 1;
			    return TxMempool.CompareIteratorByHash.InnerCompare(a, b);
		    }
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
	    private void AddTransactions(int nPackagesSelected, int nDescendantsUpdated)
	    {
		    // mapModifiedTx will store sorted packages after they are modified
		    // because some of their txs are already in the block
		    var mapModifiedTx = new Dictionary<uint256, TxMemPoolModifiedEntry>();

		    //var mapModifiedTxRes = this.mempoolScheduler.ReadAsync(() => mempool.MapTx.Values).GetAwaiter().GetResult();
		    // mapModifiedTxRes.Select(s => new TxMemPoolModifiedEntry(s)).OrderBy(o => o, new CompareModifiedEntry());

		    // Keep track of entries that failed inclusion, to avoid duplicate work
		    TxMempool.SetEntries failedTx = new TxMempool.SetEntries();

		    // Start by adding all descendants of previously added txs to mapModifiedTx
		    // and modifying them for their already included ancestors
		    UpdatePackagesForAdded(inBlock, mapModifiedTx);

		    var ancestorScoreList =
			    this.mempoolScheduler.ReadAsync(() => mempool.MapTx.AncestorScore).GetAwaiter().GetResult().ToList();

		    TxMempoolEntry iter;

		    // Limit the number of attempts to add transactions to the block when it is
		    // close to full; this is just a simple heuristic to finish quickly if the
		    // mempool has a lot of entries.
		    int MAX_CONSECUTIVE_FAILURES = 1000;
		    int nConsecutiveFailed = 0;
		    while (ancestorScoreList.Any() || mapModifiedTx.Any())
		    {
			    if (ancestorScoreList.Any())
			    {
				    var mi = ancestorScoreList.First();
				    // First try to find a new transaction in mapTx to evaluate.
				    if (mapModifiedTx.ContainsKey(mi.TransactionHash) || inBlock.Contains(mi) || failedTx.Contains(mi))
				    {
					    ancestorScoreList.Remove(mi);
					    continue;
				    }
			    }

			    // Now that mi is not stale, determine which transaction to evaluate:
			    // the next entry from mapTx, or the best from mapModifiedTx?
			    bool fUsingModified = false;
			    TxMemPoolModifiedEntry modit;
			    if (!ancestorScoreList.Any())
			    {
				    modit = mapModifiedTx.Values.OrderBy(o => new CompareModifiedEntry()).First();
				    iter = modit.iter;
				    fUsingModified = true;
			    }
			    else
			    {
				    // Try to compare the mapTx entry to the mapModifiedTx entry
				    iter = ancestorScoreList.First();
				    var compare = new CompareModifiedEntry();
				    modit = mapModifiedTx.Values.OrderBy(o => new CompareModifiedEntry()).FirstOrDefault();
				    if (modit != null && compare.Compare(modit, new TxMemPoolModifiedEntry(iter)) > 0)
				    {
					    // The best entry in mapModifiedTx has higher score
					    // than the one from mapTx.
					    // Switch which transaction (package) to consider

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
			    Guard.Assert(!inBlock.Contains(iter));

			    var packageSize = iter.SizeWithAncestors;
			    var packageFees = iter.ModFeesWithAncestors;
			    var packageSigOpsCost = iter.SizeWithAncestors;
			    if (fUsingModified)
			    {
				    packageSize = modit.SizeWithAncestors;
				    packageFees = modit.ModFeesWithAncestors;
				    packageSigOpsCost = modit.SigOpCostWithAncestors;
			    }

			    if (packageFees < blockMinFeeRate.GetFee((int) packageSize))
			    {
				    // Everything else we might consider has a lower fee rate
				    return;
			    }

			    if (!TestPackage(packageSize, packageSigOpsCost))
			    {
				    if (fUsingModified)
				    {
					    // Since we always look at the best entry in mapModifiedTx,
					    // we must erase failed entries so that we can consider the
					    // next best entry on the next loop iteration
					    mapModifiedTx.Remove(modit.iter.TransactionHash);
					    failedTx.Add(iter);
				    }

				    ++nConsecutiveFailed;

				    if (nConsecutiveFailed > MAX_CONSECUTIVE_FAILURES && nBlockWeight >
				        nBlockMaxWeight - 4000)
				    {
					    // Give up if we're close to full and haven't succeeded in a while
					    break;
				    }
				    continue;
			    }

			    TxMempool.SetEntries ancestors = new TxMempool.SetEntries();
			    long nNoLimit = long.MaxValue;
			    string dummy;
			    mempool.CalculateMemPoolAncestors(iter, ancestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);

			    OnlyUnconfirmed(ancestors);
			    ancestors.Add(iter);

			    // Test if all tx's are Final
			    if (!TestPackageTransactions(ancestors))
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
			    var sortedEntries = ancestors.ToList().OrderBy(o => new CompareTxIterByAncestorCount()).ToList();
			    foreach (var sortedEntry in sortedEntries)
			    {
				    AddToBlock(sortedEntry);
				    // Erase from the modified set, if present
				    mapModifiedTx.Remove(sortedEntry.TransactionHash);
			    }

			    ++nPackagesSelected;

			    // Update transactions that depend on each of these
			    nDescendantsUpdated += UpdatePackagesForAdded(ancestors, mapModifiedTx);
		    }
	    }
	    
		// Remove confirmed (inBlock) entries from given set 
	    private void OnlyUnconfirmed(TxMempool.SetEntries testSet)
	    {
		    foreach (var setEntry in testSet.ToList())
		    {
				// Only test txs not already in the block
				if (inBlock.Contains(setEntry))
				{
					testSet.Remove(setEntry);
				}
			}
		}

		// Test if a new package would "fit" in the block 
		private bool TestPackage(long packageSize, long packageSigOpsCost)
		{
			// TODO: switch to weight-based accounting for packages instead of vsize-based accounting.
			if (nBlockWeight + this.consensusOptions.WITNESS_SCALE_FACTOR * packageSize >= nBlockMaxWeight)
				return false;
			if (nBlockSigOpsCost + packageSigOpsCost >= this.consensusOptions.MAX_BLOCK_SIGOPS_COST)
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
			var nPotentialBlockSize = nBlockSize; // only used with fNeedSizeAccounting
		    foreach (var it in package)
		    {
				if (!it.Transaction.IsFinal(Utils.UnixTimeToDateTime(nLockTimeCutoff), nHeight))
					return false;
				if (!fIncludeWitness && it.Transaction.HasWitness)
					return false;
				if (fNeedSizeAccounting)
				{
					var nTxSize = it.Transaction.GetSerializedSize();
					if (nPotentialBlockSize + nTxSize >= nBlockMaxSize)
					{
						return false;
					}
					nPotentialBlockSize += nTxSize;
				}
			}
			return true;
		}

		/** Return true if given transaction from mapTx has already been evaluated,
		  * or if the transaction's cached data in mapTx is incorrect. */
		//private bool SkipMapTxEntry(CTxMemPool::txiter it, indexed_modified_transaction_set &mapModifiedTx, CTxMemPool::setEntries &failedTx);

		/** Sort the package in an order that is valid to appear in a block */

		private List<TxMempoolEntry> SortForBlock(TxMempool.SetEntries package)
	    {
			// Sort package by ancestor count
			// If a transaction A depends on transaction B, then A's ancestor count
			// must be greater than B's.  So this is sufficient to validly order the
			// transactions for block inclusion.
		    return package.ToList().OrderBy(o => new CompareTxIterByAncestorCount()).ToList();
		}

		// Add descendants of given transactions to mapModifiedTx with ancestor
		// state updated assuming given transactions are inBlock. Returns number
		// of updated descendants. 
		private int UpdatePackagesForAdded(TxMempool.SetEntries alreadyAdded, Dictionary<uint256, TxMemPoolModifiedEntry> mapModifiedTx)
	    {
			int descendantsUpdated = 0;
			foreach (var setEntry in alreadyAdded)
		    {
				TxMempool.SetEntries setEntries = new TxMempool.SetEntries();
				this.mempoolScheduler.ReadAsync(() => this.mempool.CalculateDescendants(setEntry, setEntries)).GetAwaiter().GetResult();
			    foreach (var desc in setEntries)
			    {
					if(alreadyAdded.Contains(desc))
						continue;
					++descendantsUpdated;
				    TxMemPoolModifiedEntry modEntry;
					if (!mapModifiedTx.TryGetValue(desc.TransactionHash, out modEntry))
						mapModifiedTx.Add(desc.TransactionHash, new TxMemPoolModifiedEntry(desc));
					modEntry.SizeWithAncestors -= setEntry.GetTxSize();
					modEntry.ModFeesWithAncestors -= setEntry.ModifiedFee;
					modEntry.SigOpCostWithAncestors -= setEntry.SigOpCost;
				}
			}
		    return descendantsUpdated;
	    }
    }
}
