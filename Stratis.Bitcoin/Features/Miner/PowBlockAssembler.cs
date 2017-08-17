using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Deployments;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
	public abstract class BlockAssembler
	{
		public abstract BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true);
	}

	public class AssemblerOptions
	{
		public long BlockMaxWeight = PowMining.DefaultBlockMaxWeight;
		public long BlockMaxSize = PowMining.DefaultBlockMaxSize;
		public FeeRate BlockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);
		public bool IsProofOfStake = false;
	};

	public class BlockTemplate
	{
		public BlockTemplate()
		{
			this.Block = new Block();
			this.VTxFees = new List<Money>();
            this.TxSigOpsCost = new List<long>();
		}

		public Block Block;
		public List<Money> VTxFees;
		public List<long> TxSigOpsCost;
		public string CoinbaseCommitment;
		public Money TotalFee;
	};

	public class PowBlockAssembler : BlockAssembler
	{
		// Unconfirmed transactions in the memory pool often depend on other
		// transactions in the memory pool. When we select transactions from the
		// pool, we select by highest fee rate of a transaction combined with all
		// its ancestors.

		static long LastBlockTx = 0;
		static long LastBlockSize = 0;
		static long LastBlockWeight = 0;

		protected readonly ConsensusLoop consensusLoop;
		protected readonly ConcurrentChain chain;
		protected readonly MempoolAsyncLock mempoolScheduler;
		protected readonly TxMempool mempool;
		protected readonly IDateTimeProvider dateTimeProvider;
	    private readonly ILogger logger;

	    protected readonly AssemblerOptions options;
		// The constructed block template
		protected readonly BlockTemplate pblocktemplate;
		// A convenience pointer that always refers to the CBlock in pblocktemplate
		protected Block pblock;

		// Configuration parameters for the block size
		private bool fIncludeWitness;
		private uint blockMaxWeight, blockMaxSize;
		private bool needSizeAccounting;
		private FeeRate blockMinFeeRate;

		// Information on the current status of the block
		private long blockWeight;
		private long blockSize;
		private long blockTx;
		private long blockSigOpsCost;
		public Money fees;
		private TxMempool.SetEntries inBlock;
		protected Transaction coinbase;

		// Chain context for the block
		protected int height;
		private long lockTimeCutoff;
		protected Network network;
		protected ChainedBlock pindexPrev;
		protected Script scriptPubKeyIn;

		public PowBlockAssembler(
            ConsensusLoop consensusLoop, 
            Network network, 
            ConcurrentChain chain,
			MempoolAsyncLock mempoolScheduler, 
            TxMempool mempool,
			IDateTimeProvider dateTimeProvider, 
            ILogger logger,
            AssemblerOptions options = null)
		{
			options = options ?? new AssemblerOptions();
			this.blockMinFeeRate = options.BlockMinFeeRate;
			// Limit weight to between 4K and MAX_BLOCK_WEIGHT-4K for sanity:
			this.blockMaxWeight = (uint)Math.Max(4000, Math.Min(PowMining.DefaultBlockMaxWeight - 4000, options.BlockMaxWeight));
			// Limit size to between 1K and MAX_BLOCK_SERIALIZED_SIZE-1K for sanity:
			this.blockMaxSize = (uint)Math.Max(1000, Math.Min(network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_SERIALIZED_SIZE - 1000, options.BlockMaxSize));
			// Whether we need to account for byte usage (in addition to weight usage)
			this.needSizeAccounting = (this.blockMaxSize < network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_SERIALIZED_SIZE - 1000);

			this.consensusLoop = consensusLoop;
			this.chain = chain;
			this.mempoolScheduler = mempoolScheduler;
			this.mempool = mempool;
			this.dateTimeProvider = dateTimeProvider;
		    this.logger = logger;
		    this.options = options;
			this.network = network;

			this.inBlock = new TxMempool.SetEntries();

			// Reserve space for coinbase tx
			this.blockSize = 1000;
			this.blockWeight = 4000;
			this.blockSigOpsCost = 400;
			this.fIncludeWitness = false;

			// These counters do not include coinbase tx
			this.blockTx = 0;
			this.fees = 0;

			this.pblocktemplate = new BlockTemplate {Block = new Block(), VTxFees = new List<Money>()};
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

			return (int) nVersion;
		}

		private static long medianTimePast;
		const long TicksPerMicrosecond = 10;
		/** Construct a new block template with coinbase to scriptPubKeyIn */

		public override BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
		{
			long nTimeStart = DateTime.UtcNow.Ticks/TicksPerMicrosecond;
            this.pblock = this.pblocktemplate.Block; // pointer for convenience
			this.scriptPubKeyIn = scriptPubKeyIn;

			this.CreateCoinbase();
			this.ComputeBlockVersion();


			// TODO: MineBlocksOnDemand
			// -regtest only: allow overriding block.nVersion with
			// -blockversion=N to test forking scenarios
			//if (this.network. chainparams.MineBlocksOnDemand())
			//	pblock->nVersion = GetArg("-blockversion", pblock->nVersion);

			medianTimePast = Utils.DateTimeToUnixTime(this.pindexPrev.GetMedianTimePast());
            this.lockTimeCutoff = PowConsensusValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast)
				? medianTimePast
				: this.pblock.Header.Time;

            // TODO: Implement Witness Code
            // Decide whether to include witness transactions
            // This is only needed in case the witness softfork activation is reverted
            // (which would require a very deep reorganization) or when
            // -promiscuousmempoolflags is used.
            // TODO: replace this with a call to main to assess validity of a mempool
            // transaction (which in most cases can be a no-op).
            this.fIncludeWitness = false; //IsWitnessEnabled(pindexPrev, chainparams.GetConsensus()) && fMineWitnessTx;

			// add transactions from the mempool
			int nPackagesSelected = 0;
			int nDescendantsUpdated = 0;
			this.AddTransactions(nPackagesSelected, nDescendantsUpdated);

			long nTime1 = DateTime.UtcNow.Ticks/TicksPerMicrosecond;
			LastBlockTx = this.blockTx;
			LastBlockSize = this.blockSize;
			LastBlockWeight = this.blockWeight;

			// TODO: Implement Witness Code
			// pblocktemplate->CoinbaseCommitment = GenerateCoinbaseCommitment(*pblock, pindexPrev, chainparams.GetConsensus());
			this.pblocktemplate.VTxFees[0] = -this.fees;
			this.coinbase.Outputs[0].Value = this.fees + this.consensusLoop.Validator.GetProofOfWorkReward(this.height);
            this.pblocktemplate.TotalFee = this.fees;

			var nSerializeSize = this.pblock.GetSerializedSize();
			this.logger.LogInformation(
				$"CreateNewBlock: total size: {nSerializeSize} block weight: {this.consensusLoop.Validator.GetBlockWeight(this.pblock)} txs: {this.blockTx} fees: {this.fees} sigops {this.blockSigOpsCost}");

			this.UpdateHeaders();

			//pblocktemplate->TxSigOpsCost[0] = WITNESS_SCALE_FACTOR * GetLegacySigOpCount(*pblock->vtx[0]);

			this.TestBlockValidity();

			//int64_t nTime2 = GetTimeMicros();

			//LogPrint(BCLog::BENCH, "CreateNewBlock() packages: %.2fms (%d packages, %d updated descendants), validity: %.2fms (total %.2fms)\n", 0.001 * (nTime1 - nTimeStart), nPackagesSelected, nDescendantsUpdated, 0.001 * (nTime2 - nTime1), 0.001 * (nTime2 - nTimeStart));

			return this.pblocktemplate;
		}

		protected virtual void ComputeBlockVersion()
		{
			// compute the block version
			this.pindexPrev = this.chain.Tip;
			this.height = this.pindexPrev.Height + 1;
            this.pblock.Header.Version = this.ComputeBlockVersion(this.pindexPrev, this.network.Consensus);
		}

		protected virtual void CreateCoinbase()
		{
			// Create coinbase transaction.
			// set the coin base with zero money 
			// once we have the fee we can update the amount
			this.coinbase = new Transaction();
			this.coinbase.AddInput(TxIn.CreateCoinbase(this.chain.Height + 1));
			this.coinbase.AddOutput(new TxOut(Money.Zero, this.scriptPubKeyIn));
			this.pblock.AddTransaction(this.coinbase);
			this.pblocktemplate.VTxFees.Add(-1); // updated at end
            this.pblocktemplate.TxSigOpsCost.Add(-1); // updated at end

		}

		protected virtual void UpdateHeaders()
		{
			// Fill in header
			this.pblock.Header.HashPrevBlock = this.pindexPrev.HashBlock;
			this.pblock.Header.UpdateTime(this.dateTimeProvider.GetTimeOffset(), this.network, this.chain.Tip);
			this.pblock.Header.Bits = this.pblock.Header.GetWorkRequired(this.network, this.chain.Tip);
            this.pblock.Header.Nonce = 0;
		}

		protected virtual void TestBlockValidity()
		{
			var context = new ContextInformation(new BlockResult {Block = this.pblock }, this.network.Consensus)
			{
				CheckPow = false,
				CheckMerkleRoot = false,
				OnlyCheck = true
			};

			this.consensusLoop.AcceptBlock(context);
		}

		// Add a tx to the block 
		private void AddToBlock(TxMempoolEntry iter)
	    {
            this.pblock.AddTransaction(iter.Transaction);

		    this.pblocktemplate.VTxFees.Add(iter.Fee);
            this.pblocktemplate.TxSigOpsCost.Add(iter.SigOpCost);
		    if (this.needSizeAccounting)
		    {
                this.blockSize += iter.Transaction.GetSerializedSize();
		    }
            this.blockWeight += iter.TxWeight;
		    ++this.blockTx;
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
	    }

	    // Container for tracking updates to ancestor feerate as we include (parent)
		// transactions in a block
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
	    protected virtual void AddTransactions(int nPackagesSelected, int nDescendantsUpdated)
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
		    this.UpdatePackagesForAdded(this.inBlock, mapModifiedTx);

		    var ancestorScoreList =
			    this.mempoolScheduler.ReadAsync(() => this.mempool.MapTx.AncestorScore).GetAwaiter().GetResult().ToList();

		    TxMempoolEntry iter;

		    // Limit the number of attempts to add transactions to the block when it is
		    // close to full; this is just a simple heuristic to finish quickly if the
		    // mempool has a lot of entries.
		    int MAX_CONSECUTIVE_FAILURES = 1000;
		    int nConsecutiveFailed = 0;
		    while (ancestorScoreList.Any() || mapModifiedTx.Any())
		    {
				var mi = ancestorScoreList.FirstOrDefault();
				if (mi != null)
			    {
					// Skip entries in mapTx that are already in a block or are present
					// in mapModifiedTx (which implies that the mapTx ancestor state is
					// stale due to ancestor inclusion in the block)
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
			    Guard.Assert(!this.inBlock.Contains(iter));

			    var packageSize = iter.SizeWithAncestors;
			    var packageFees = iter.ModFeesWithAncestors;
			    var packageSigOpsCost = iter.SizeWithAncestors;
			    if (fUsingModified)
			    {
				    packageSize = modit.SizeWithAncestors;
				    packageFees = modit.ModFeesWithAncestors;
				    packageSigOpsCost = modit.SigOpCostWithAncestors;
			    }

			    if (packageFees < this.blockMinFeeRate.GetFee((int) packageSize))
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

				    ++nConsecutiveFailed;

				    if (nConsecutiveFailed > MAX_CONSECUTIVE_FAILURES && this.blockWeight >
                        this.blockMaxWeight - 4000)
				    {
					    // Give up if we're close to full and haven't succeeded in a while
					    break;
				    }
				    continue;
			    }

			    TxMempool.SetEntries ancestors = new TxMempool.SetEntries();
			    long nNoLimit = long.MaxValue;
			    string dummy;
                this.mempool.CalculateMemPoolAncestors(iter, ancestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);

			    this.OnlyUnconfirmed(ancestors);
			    ancestors.Add(iter);

			    // Test if all tx's are Final
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
			    var sortedEntries = ancestors.ToList().OrderBy(o => o, new CompareTxIterByAncestorCount()).ToList();
			    foreach (var sortedEntry in sortedEntries)
			    {
				    this.AddToBlock(sortedEntry);
				    // Erase from the modified set, if present
				    mapModifiedTx.Remove(sortedEntry.TransactionHash);
			    }

			    ++nPackagesSelected;

			    // Update transactions that depend on each of these
			    nDescendantsUpdated += this.UpdatePackagesForAdded(ancestors, mapModifiedTx);
		    }
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

		// Test if a new package would "fit" in the block 
		private bool TestPackage(long packageSize, long packageSigOpsCost)
		{
			// TODO: switch to weight-based accounting for packages instead of vsize-based accounting.
			if (this.blockWeight + this.network.Consensus.Option<PowConsensusOptions>().WITNESS_SCALE_FACTOR * packageSize >= this.blockMaxWeight)
				return false;
			if (this.blockSigOpsCost + packageSigOpsCost >= this.network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_SIGOPS_COST)
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
			var nPotentialBlockSize = this.blockSize; // only used with needSizeAccounting
		    foreach (var it in package)
		    {
				if (!it.Transaction.IsFinal(Utils.UnixTimeToDateTime(this.lockTimeCutoff), this.height))
					return false;
				if (!this.fIncludeWitness && it.Transaction.HasWitness)
					return false;
				if (this.needSizeAccounting)
				{
					var nTxSize = it.Transaction.GetSerializedSize();
					if (nPotentialBlockSize + nTxSize >= this.blockMaxSize)
					{
						return false;
					}
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
