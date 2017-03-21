using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool.Fee;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	// Information about a mempool transaction.
	public class TxMempoolInfo
	{
		// The transaction itself 
		public Transaction Trx { get; set; }

		// Time the transaction entered the mempool. 
		public long Time { get; set; }

		// Feerate of the transaction. 
		public FeeRate FeeRate { get; set; }

		// The fee delta. 
		public long FeeDelta { get; set; }
	};

	/**
	* CTxMemPool stores valid-according-to-the-current-best-chain transactions
	* that may be included in the next block.
	*
	* Transactions are added when they are seen on the network (or created by the
	* local node), but not all transactions seen are added to the pool. For
	* example, the following new transactions will not be added to the mempool:
	* - a transaction which doesn't make the mimimum fee requirements.
	* - a new transaction that double-spends an input of a transaction already in
	* the pool where the new transaction does not meet the Replace-By-Fee
	* requirements as defined in BIP 125.
	* - a non-standard transaction.
	*
	* CTxMemPool::mapTx, and CTxMemPoolEntry bookkeeping:
	*
	* mapTx is a boost::multi_index that sorts the mempool on 4 criteria:
	* - transaction hash
	* - feerate [we use max(feerate of tx, feerate of Transaction with all descendants)]
	* - time in mempool
	* - mining score (feerate modified by any fee deltas from PrioritiseTransaction)
	*
	* Note: the term "descendant" refers to in-mempool transactions that depend on
	* this one, while "ancestor" refers to in-mempool transactions that a given
	* transaction depends on.
	*
	* In order for the feerate sort to remain correct, we must update transactions
	* in the mempool when new descendants arrive.  To facilitate this, we track
	* the set of in-mempool direct parents and direct children in mapLinks.  Within
	* each CTxMemPoolEntry, we track the size and fees of all descendants.
	*
	* Usually when a new transaction is added to the mempool, it has no in-mempool
	* children (because any such children would be an orphan).  So in
	* addUnchecked(), we:
	* - update a new entry's setMemPoolParents to include all in-mempool parents
	* - update the new entry's direct parents to include the new tx as a child
	* - update all ancestors of the transaction to include the new tx's size/fee
	*
	* When a transaction is removed from the mempool, we must:
	* - update all in-mempool parents to not track the tx in setMemPoolChildren
	* - update all ancestors to not include the tx's size/fees in descendant state
	* - update all in-mempool children to not include it as a parent
	*
	* These happen in UpdateForRemoveFromMempool().  (Note that when removing a
	* transaction along with its descendants, we must calculate that set of
	* transactions to be removed before doing the removal, or else the mempool can
	* be in an inconsistent state where it's impossible to walk the ancestors of
	* a transaction.)
	*
	* In the event of a reorg, the assumption that a newly added tx has no
	* in-mempool children is false.  In particular, the mempool is in an
	* inconsistent state while new transactions are being added, because there may
	* be descendant transactions of a tx coming from a disconnected block that are
	* unreachable from just looking at transactions in the mempool (the linking
	* transactions may also be in the disconnected block, waiting to be added).
	* Because of this, there's not much benefit in trying to search for in-mempool
	* children in addUnchecked().  Instead, in the special case of transactions
	* being added from a disconnected block, we require the caller to clean up the
	* state, to account for in-mempool, out-of-block descendants for all the
	* in-block transactions by calling UpdateTransactionsFromBlock().  Note that
	* until this is called, the mempool state is not consistent, and in particular
	* mapLinks may not be correct (and therefore functions like
	* CalculateMemPoolAncestors() and CalculateDescendants() that rely
	* on them to walk the mempool are not generally safe to use).
	*
	* Computational limits:
	*
	* Updating all in-mempool ancestors of a newly added transaction can be slow,
	* if no bound exists on how many in-mempool ancestors there may be.
	* CalculateMemPoolAncestors() takes configurable limits that are designed to
	* prevent these calculations from being too CPU intensive.
	*
	* Adding transactions from a disconnected block can be very time consuming,
	* because we don't have a way to limit the number of in-mempool descendants.
	* To bound CPU processing, we limit the amount of work we're willing to do
	* to properly update the descendant information for a tx being added from
	* a disconnected block.  If we would exceed the limit, then we instead mark
	* the entry as "dirty", and set the feerate for sorting purposes to be equal
	* the feerate of the transaction without any descendants.
	*
	*/
	public class TxMempool
	{
		// Fake height value used in CCoins to signify they are only in the memory pool (since 0.8) 
		public const int MempoolHeight = 0x7FFFFFFF;

		private double checkFrequency; //!< Value n means that n times in 2^32 we check.
		private int nTransactionsUpdated;
		public BlockPolicyEstimator MinerPolicyEstimator { get; }

		long totalTxSize;      //!< sum of all mempool tx's virtual sizes. Differs from serialized Transaction size since witness data is discounted. Defined in BIP 141.
		long cachedInnerUsage; //!< sum of dynamic memory usage of all the map elements (NOT the maps themselves)

		readonly FeeRate minReasonableRelayFee;

		long lastRollingFeeUpdate;
		bool blockSinceLastRollingFeeBump;
		double rollingMinimumFeeRate; //!< minimum fee to get into the pool, decreases exponentially

		public const int RollingFeeHalflife = 60 * 60 * 12; // public only for testing

		public class IndexedTransactionSet : Dictionary<uint256, TxMempoolEntry>
		{
			public IndexedTransactionSet() : base(new SaltedTxidHasher())
			{
			}

			public void Add(TxMempoolEntry entry)
			{
				this.Add(entry.TransactionHash, entry);
			}

			public void Remove(TxMempoolEntry entry)
			{
				this.Remove(entry.TransactionHash);
			}

			public IEnumerable<TxMempoolEntry> DescendantScore
			{
				get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByDescendantScore()); }
			}

			public IEnumerable<TxMempoolEntry> EntryTime
			{
				get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByEntryTime()); }
			}

			public IEnumerable<TxMempoolEntry> MiningScore
			{
				get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByScore()); }
			}

			public IEnumerable<TxMempoolEntry> AncestorScore
			{
				get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByAncestorFee()); }
			}
		
			private class SaltedTxidHasher : IEqualityComparer<uint256>
			{
				public bool Equals(uint256 x, uint256 y)
				{
					return x == y;
				}

				public int GetHashCode(uint256 obj)
				{
					// todo: need to compare with the c++ implementation
					return obj.GetHashCode();
				}
			}

			/** \class CompareTxMemPoolEntryByDescendantScore
			*
			*  Sort an entry by max(score/size of entry's tx, score/size with all descendants).
			*/

			private class CompareTxMemPoolEntryByDescendantScore : IComparer<TxMempoolEntry>
			{
				public int Compare(TxMempoolEntry a, TxMempoolEntry b)
				{
					bool fUseADescendants = UseDescendantScore(a);
					bool fUseBDescendants = UseDescendantScore(b);

					double aModFee = fUseADescendants ? a.ModFeesWithDescendants.Satoshi : a.ModifiedFee;
					double aSize = fUseADescendants ? a.SizeWithDescendants : a.GetTxSize();

					double bModFee = fUseBDescendants ? b.ModFeesWithDescendants.Satoshi : b.ModifiedFee;
					double bSize = fUseBDescendants ? b.SizeWithDescendants : b.GetTxSize();

					// Avoid division by rewriting (a/b > c/d) as (a*d > c*b).
					double f1 = aModFee*bSize;
					double f2 = aSize*bModFee;

					if (f1 == f2)
					{
						if (a.Time >= b.Time)
							return -1;
						return 1;
					}

					if (f1 <= f2)
						return -1;
					return 1;
				}

				// Calculate which score to use for an entry (avoiding division).

				bool UseDescendantScore(TxMempoolEntry a)
				{
					double f1 = (double) a.ModifiedFee*a.SizeWithDescendants;
					double f2 = (double) a.ModFeesWithDescendants.Satoshi*a.GetTxSize();
					return f2 > f1;
				}
			}

			private class CompareTxMemPoolEntryByEntryTime : IComparer<TxMempoolEntry>
			{
				public int Compare(TxMempoolEntry a, TxMempoolEntry b)
				{
					if (a.Time < b.Time)
						return -1;
					return 1;
				}
			}

			/** \class CompareTxMemPoolEntryByScore
			*
			*  Sort by score of entry ((fee+delta)/size) in descending order
			*/
			private class CompareTxMemPoolEntryByScore : IComparer<TxMempoolEntry>
			{
				public int Compare(TxMempoolEntry a, TxMempoolEntry b)
				{
					double f1 = (double) a.ModifiedFee*b.GetTxSize();
					double f2 = (double) b.ModifiedFee*a.GetTxSize();
					if (f1 == f2)
					{
						if (a.TransactionHash < b.TransactionHash)
							return 1;
						return -1;
					}
					if (f1 > f2)
						return -1;
					return 1;
				}
			}

			private class CompareTxMemPoolEntryByAncestorFee : IComparer<TxMempoolEntry>
			{
				public int Compare(TxMempoolEntry a, TxMempoolEntry b)
				{
					double aFees = a.ModFeesWithAncestors.Satoshi;
					double aSize = a.SizeWithAncestors;

					double bFees = b.ModFeesWithAncestors.Satoshi;
					double bSize = b.SizeWithAncestors;

					// Avoid division by rewriting (a/b > c/d) as (a*d > c*b).
					double f1 = aFees*bSize;
					double f2 = aSize*bFees;

					if (f1 == f2)
					{
						if (a.TransactionHash < b.TransactionHash)
							return -1;
						return 1;
					}

					if (f1 > f2)
						return -1;
					return 1;
				}
			}
		}

		private class CompareIteratorByHash : IComparer<TxMempoolEntry>
		{
			public int Compare(TxMempoolEntry a, TxMempoolEntry b)
			{
				if (a.TransactionHash == b.TransactionHash) return 0;
				if (a.TransactionHash < b.TransactionHash) return -1;
				return 1;
			}
		}
		public class TxLinks
		{
			public SetEntries Parents;
			public SetEntries Children;
		};

		public class SetEntries : SortedSet<TxMempoolEntry>, IEquatable<SetEntries>, IEqualityComparer<TxMempoolEntry>

		{
			public SetEntries() : base(new CompareIteratorByHash())
			{
			}

			public bool Equals(SetEntries other)
			{
				return this.SequenceEqual(other, this);
			}

			public bool Equals(TxMempoolEntry x, TxMempoolEntry y)
			{
				return x.TransactionHash == y.TransactionHash;
			}

			public int GetHashCode(TxMempoolEntry obj)
			{
				return obj?.TransactionHash?.GetHashCode() ?? 0;
			}
		}

		public class TxlinksMap : SortedList<TxMempoolEntry, TxLinks>
		{
			public TxlinksMap() : base(new CompareIteratorByHash())
			{
			}
		}

		public class DeltaPair
		{
			public double Delta;
			public Money Amount;
		}

		public class NextTxPair
		{
			public OutPoint OutPoint;
			public Transaction Transaction;
		}

		public IndexedTransactionSet MapTx;
		private TxlinksMap mapLinks;
		public List<NextTxPair> MapNextTx;
		private Dictionary<uint256, DeltaPair> mapDeltas;
		private Dictionary<TxMempoolEntry, uint256> vTxHashes;  //!< All tx witness hashes/entries in mapTx, in random order
		private DateTimeProvider TimeProvider { get; }

		public TxMempool(FeeRate minReasonableRelayFee, NodeSettings nodeArgs) : this(minReasonableRelayFee, DateTimeProvider.Default, nodeArgs)
		{
		}

		/** Create a new CTxMemPool.
		*  minReasonableRelayFee should be a feerate which is, roughly, somewhere
		*  around what it "costs" to relay a transaction around the network and
		*  below which we would reasonably say a transaction has 0-effective-fee.
		*/
		public TxMempool(FeeRate minReasonableRelayFee, DateTimeProvider dateTimeProvider, NodeSettings nodeArgs)
		{
			this.MapTx = new IndexedTransactionSet();
			this.mapLinks = new TxlinksMap();
			this.MapNextTx = new List<NextTxPair>();
			this.mapDeltas = new Dictionary<uint256, DeltaPair>();
			this.vTxHashes = new Dictionary<TxMempoolEntry, uint256>(); //!< All tx witness hashes/entries in mapTx, in random order

			this.TimeProvider = dateTimeProvider;
			this.InnerClear(); //lock free clear

			// Sanity checks off by default for performance, because otherwise
			// accepting transactions becomes O(N^2) where N is the number
			// of transactions in the pool
			this.checkFrequency = 0;

			this.MinerPolicyEstimator = new BlockPolicyEstimator(minReasonableRelayFee, nodeArgs);
			this.minReasonableRelayFee = minReasonableRelayFee;
		}

		private void InnerClear()
		{
			mapLinks.Clear();
			MapTx.Clear();
			MapNextTx.Clear();
			totalTxSize = 0;
			cachedInnerUsage = 0;
			lastRollingFeeUpdate = this.TimeProvider.GetTime();
			blockSinceLastRollingFeeBump = false;
			rollingMinimumFeeRate = 0;
			++nTransactionsUpdated;
		}

		public void Clear()
		{
			//LOCK(cs);
			this.InnerClear();
		}

		private void trackPackageRemoved(FeeRate rate)
		{
			// candidate for async
			//AssertLockHeld(cs);

			if (rate.FeePerK.Satoshi > rollingMinimumFeeRate)
			{
				rollingMinimumFeeRate = rate.FeePerK.Satoshi;
				blockSinceLastRollingFeeBump = false;
			}
		}

		/**
		 * If sanity-checking is turned on, check makes sure the pool is
		 * consistent (does not contain two transactions that spend the same inputs,
		 * all inputs are in the mapNextTx array). If sanity-checking is turned off,
		 * check does nothing.
		 */
		public void Check(CoinView pcoins)
		{
			if (this.checkFrequency == 0)
				return;

			if (new Random(int.MaxValue).Next() >= checkFrequency)
				return;

			Logging.Logs.Mempool.LogInformation($"Checking mempool with {this.MapTx.Count} transactions and {this.MapNextTx.Count} inputs");

			throw new NotImplementedException();
		}

		public Transaction Get(uint256 hash)
		{
			return this.MapTx.TryGet(hash)?.Transaction;
		}

		public FeeRate EstimateFee(int nBlocks)
		{

			return MinerPolicyEstimator.EstimateFee(nBlocks);
		}

		public FeeRate EstimateSmartFee(int nBlocks, out int answerFoundAtBlocks)
		{

			return MinerPolicyEstimator.EstimateSmartFee(nBlocks, this, out answerFoundAtBlocks);
		}

		public double EstimatePriority(int nBlocks)
		{

			return MinerPolicyEstimator.EstimatePriority(nBlocks);
		}

		public double EstimateSmartPriority(int nBlocks, out int answerFoundAtBlocks)
		{

			return MinerPolicyEstimator.EstimateSmartPriority(nBlocks, this, out answerFoundAtBlocks);
		}

		public void SetSanityCheck(double dFrequency = 1.0) { checkFrequency = dFrequency * 4294967295.0; }

		// addUnchecked must updated state for all ancestors of a given transaction,
		// to track size/count of descendant transactions.  First version of
		// addUnchecked can be used to have it call CalculateMemPoolAncestors(), and
		// then invoke the second version.
		public bool AddUnchecked(uint256 hash, TxMempoolEntry entry, bool validFeeEstimate = true)
		{
			//LOCK(cs);
			SetEntries setAncestors = new SetEntries();
			long nNoLimit = long.MaxValue;
			string dummy;
			CalculateMemPoolAncestors(entry, setAncestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy);
			return AddUnchecked(hash, entry, setAncestors, validFeeEstimate);

		}

		public bool AddUnchecked(uint256 hash, TxMempoolEntry entry, SetEntries setAncestors, bool validFeeEstimate = true)
		{
			// Add to memory pool without checking anything.
			// Used by main.cpp AcceptToMemoryPool(), which DOES do
			// all the appropriate checks.
			//LOCK(cs);
			MapTx.Add(entry);
			mapLinks.Add(entry, new TxLinks {Parents = new SetEntries(), Children = new SetEntries()});

			// Update transaction for any feeDelta created by PrioritiseTransaction
			// TODO: refactor so that the fee delta is calculated before inserting
			// into mapTx.
			var pos = mapDeltas.TryGet(hash);
			if (pos != null)
			{
				if (pos.Amount != null)
				{
					entry.UpdateFeeDelta(pos.Amount.Satoshi);
				}
			}

			// Update cachedInnerUsage to include contained transaction's usage.
			// (When we update the entry for in-mempool parents, memory usage will be
			// further updated.)
			cachedInnerUsage += entry.DynamicMemoryUsage();

			var tx = entry.Transaction;
			HashSet<uint256> setParentTransactions = new HashSet<uint256>();
			foreach (var txInput in tx.Inputs)
			{

				MapNextTx.Add(new NextTxPair {OutPoint = txInput.PrevOut, Transaction = tx});
				setParentTransactions.Add(txInput.PrevOut.Hash);
			}
			// Don't bother worrying about child transactions of this one.
			// Normal case of a new transaction arriving is that there can't be any
			// children, because such children would be orphans.
			// An exception to that is if a transaction enters that used to be in a block.
			// In that case, our disconnect block logic will call UpdateTransactionsFromBlock
			// to clean up the mess we're leaving here.

			// Update ancestors with information about this tx
			foreach (var phash in setParentTransactions)
			{
				var pit = MapTx.TryGet(phash);
				if (pit != null)
					UpdateParent(entry, pit, true);
			}

			UpdateAncestorsOf(true, entry, setAncestors);
			UpdateEntryForAncestors(entry, setAncestors);

			nTransactionsUpdated++;
			totalTxSize += entry.GetTxSize();

			this.MinerPolicyEstimator.ProcessTransaction(entry, validFeeEstimate);

			vTxHashes.Add(entry, tx.GetWitHash());
			//entry.vTxHashesIdx = vTxHashes.size() - 1;

			return true;
		}

		/** Set ancestor state for an entry */

		void UpdateEntryForAncestors(TxMempoolEntry it, SetEntries setAncestors)
		{
			long updateCount = setAncestors.Count;
			long updateSize = 0;
			Money updateFee = 0;
			long updateSigOpsCost = 0;
			foreach (var ancestorIt in setAncestors)
			{

				updateSize += ancestorIt.GetTxSize();
				updateFee += ancestorIt.ModifiedFee;
				updateSigOpsCost += ancestorIt.SigOpCost;
			}
			it.UpdateAncestorState(updateSize, updateFee, updateCount, updateSigOpsCost);
		}

		/** Update ancestors of hash to add/remove it as a descendant transaction. */
		private void UpdateAncestorsOf(bool add, TxMempoolEntry it, SetEntries setAncestors)
		{
			SetEntries parentIters = GetMemPoolParents(it);
			// add or remove this tx as a child of each parent
			foreach (var piter in parentIters)
				UpdateChild(piter, it, add);

			long updateCount = (add ? 1 : -1);
			long updateSize = updateCount*it.GetTxSize();
			Money updateFee = updateCount*it.ModifiedFee;
			foreach (var ancestorIt in setAncestors)
			{
				ancestorIt.UpdateDescendantState(updateSize, updateFee, updateCount);
			}
		}

		private SetEntries GetMemPoolParents(TxMempoolEntry entry)
		{
			Guard.NotNull(entry, nameof(entry));

			Utilities.Guard.Assert(MapTx.ContainsKey(entry.TransactionHash));
			var it = mapLinks.TryGet(entry);
			Utilities.Guard.Assert(it != null);
			return it.Parents;
		}

		private SetEntries GetMemPoolChildren(TxMempoolEntry entry)
		{
			Guard.NotNull(entry, nameof(entry));
			
			Utilities.Guard.Assert(MapTx.ContainsKey(entry.TransactionHash));
			var it = mapLinks.TryGet(entry);
			Utilities.Guard.Assert(it != null);
			return it.Children;
		}

		private void UpdateChild(TxMempoolEntry entry, TxMempoolEntry child, bool add)
		{
			// todo: find how to take a memory size of SetEntries
			//setEntries s;
			if (add && mapLinks[entry].Children.Add(child))
			{
				cachedInnerUsage += child.DynamicMemoryUsage();
			}
			else if (!add && mapLinks[entry].Children.Remove(child))
			{
				cachedInnerUsage -= child.DynamicMemoryUsage();
			}
		}

		private void UpdateParent(TxMempoolEntry entry, TxMempoolEntry parent, bool add)
		{
			// todo: find how to take a memory size of SetEntries
			//SetEntries s;
			if (add && mapLinks[entry].Parents.Add(parent))
			{
				cachedInnerUsage += parent.DynamicMemoryUsage();
			}
			else if (!add && mapLinks[entry].Parents.Remove(parent))
			{
				cachedInnerUsage -= parent.DynamicMemoryUsage();
			}
		}

		/** Try to calculate all in-mempool ancestors of entry.
		 *  (these are all calculated including the tx itself)
		 *  limitAncestorCount = max number of ancestorsUpdateTransactionsFromBlock
		 *  limitAncestorSize = max size of ancestors
		 *  limitDescendantCount = max number of descendants any ancestor can have
		 *  limitDescendantSize = max size of descendants any ancestor can have
		 *  errString = populated with error reason if any limits are hit
		 *  fSearchForParents = whether to search a tx's vin for in-mempool parents, or
		 *    look up parents from mapLinks. Must be true for entries not in the mempool
		 */
		public bool CalculateMemPoolAncestors(TxMempoolEntry entry, SetEntries setAncestors, long limitAncestorCount,
			long limitAncestorSize, long limitDescendantCount, long limitDescendantSize, out string errString,
			bool fSearchForParents = true)
		{
			errString = string.Empty;
			SetEntries parentHashes = new SetEntries();
			var tx = entry.Transaction;

			if (fSearchForParents)
			{
				// Get parents of this transaction that are in the mempool
				// GetMemPoolParents() is only valid for entries in the mempool, so we
				// iterate mapTx to find parents.
				foreach (var txInput in tx.Inputs)
				{
					var piter = MapTx.TryGet(txInput.PrevOut.Hash);
					if (piter != null)
					{
						parentHashes.Add(piter);
						if (parentHashes.Count + 1 > limitAncestorCount)
						{
							errString = $"too many unconfirmed parents [limit: {limitAncestorCount}]";
							return false;
						}
					}
				}
			}
			else
			{
				// If we're not searching for parents, we require this to be an
				// entry in the mempool already.
				//var it = mapTx.Txids.TryGet(entry.TransactionHash);
				var memPoolParents = GetMemPoolParents(entry);
				foreach (var item in memPoolParents)
					parentHashes.Add(item);
			}

			var totalSizeWithAncestors = entry.GetTxSize();

			while (parentHashes.Any())
			{
				var stageit = parentHashes.First();

				setAncestors.Add(stageit);
				parentHashes.Remove(stageit);
				totalSizeWithAncestors += stageit.GetTxSize();

				if (stageit.SizeWithDescendants + entry.GetTxSize() > limitDescendantSize)
				{
					errString = $"exceeds descendant size limit for tx {stageit.TransactionHash} [limit: {limitDescendantSize}]";
					return false;
				}
				else if (stageit.CountWithDescendants + 1 > limitDescendantCount)
				{
					errString = $"too many descendants for tx {stageit.TransactionHash} [limit: {limitDescendantCount}]";
					return false;
				}
				else if (totalSizeWithAncestors > limitAncestorSize)
				{
					errString = $"exceeds ancestor size limit [limit: {limitAncestorSize}]";
					return false;
				}

				var setMemPoolParents = GetMemPoolParents(stageit);
				foreach (var phash in setMemPoolParents)
				{
					// If this is a new ancestor, add it.
					if (!setAncestors.Contains(phash))
					{
						parentHashes.Add(phash);
					}
					if (parentHashes.Count + setAncestors.Count + 1 > limitAncestorCount)
					{
						errString = $"too many unconfirmed ancestors [limit: {limitAncestorCount}]";
						return false;
					}
				}
			}

			return true;
		}

		
		//  Check that none of this transactions inputs are in the mempool, and thus
		//  the tx is not dependent on other mempool transactions to be included in a block.
		 public bool HasNoInputsOf(Transaction tx)
		{
			foreach (var txInput in tx.Inputs)
				if (this.Exists(txInput.PrevOut.Hash))
					return false;
			return true;
		}

		public bool Exists(uint256 hash)
		{
			return MapTx.ContainsKey(hash);
		}

		public long Size
		{
			get { return this.MapTx.Count; }
		}

		public void RemoveRecursive(Transaction origTx)
		{
			// Remove transaction from memory pool
			var origHahs = origTx.GetHash();

			SetEntries txToRemove = new SetEntries();
			var origit = MapTx.TryGet(origHahs);
			if (origit != null)
			{
				txToRemove.Add(origit);
			}
			else
			{
				// When recursively removing but origTx isn't in the mempool
				// be sure to remove any children that are in the pool. This can
				// happen during chain re-orgs if origTx isn't re-accepted into
				// the mempool for any reason.
				for (int i = 0; i < origTx.Outputs.Count; i++)
				{
					var it = MapNextTx.FirstOrDefault(w => w.OutPoint == new OutPoint(origHahs, i));
					if (it == null)
						continue;
					var nextit = MapTx.TryGet(it.Transaction.GetHash());
					Utilities.Guard.Assert(nextit != null);
					txToRemove.Add(nextit);
				}
			}
			SetEntries setAllRemoves = new SetEntries();

			foreach (var item in txToRemove)
			{


				CalculateDescendants(item, setAllRemoves);
			}

			RemoveStaged(setAllRemoves, false);
		}

		/** Remove a set of transactions from the mempool.
		 *  If a transaction is in this set, then all in-mempool descendants must
		 *  also be in the set, unless this transaction is being removed for being
		 *  in a block.
		 *  Set updateDescendants to true when removing a tx that was in a block, so
		 *  that any in-mempool descendants have their ancestor state updated.
		 */
		public void RemoveStaged(SetEntries stage, bool updateDescendants)
		{
			//AssertLockHeld(cs);
			UpdateForRemoveFromMempool(stage, updateDescendants);
			foreach (var it in stage)
			{ 
				RemoveUnchecked(it);
			}
		}

		// Expire all transaction (and their dependencies) in the mempool older than time. Return the number of removed transactions. 
		public int Expire(long time)
		{
			//LOCK(cs);
			SetEntries toremove = new SetEntries();
			foreach (var entry in this.MapTx.EntryTime)
			{
				if (!(entry.Time < time)) break;
				toremove.Add(entry);
			}

			SetEntries stage = new SetEntries();
			foreach (var removeit in toremove)
			{
				CalculateDescendants(removeit, stage);
			}
			RemoveStaged(stage, false);
			return stage.Count;
		}

		/** Before calling removeUnchecked for a given transaction,
		 *  UpdateForRemoveFromMempool must be called on the entire (dependent) set
		 *  of transactions being removed at the same time.  We use each
		 *  CTxMemPoolEntry's setMemPoolParents in order to walk ancestors of a
		 *  given transaction that is removed, so we can't remove intermediate
		 *  transactions in a chain before we've updated all the state for the
		 *  removal.
		 */
		private void RemoveUnchecked(TxMempoolEntry it)
		{
			var hash = it.TransactionHash;
			foreach (var txin in it.Transaction.Inputs)
			{
				MapNextTx.Remove(MapNextTx.FirstOrDefault(w => w.OutPoint == txin.PrevOut));
			}
			
			if (vTxHashes.Any())
			{
				vTxHashes.Remove(it);

				//vTxHashes[it] = std::move(vTxHashes.back());
				//vTxHashes[it].second->vTxHashesIdx = it->vTxHashesIdx;
				//vTxHashes.pop_back();
				//if (vTxHashes.size() * 2 < vTxHashes.capacity())
				//	vTxHashes.shrink_to_fit();
			}
			//else
			//	vTxHashes.clear();

			totalTxSize -= it.GetTxSize();
			cachedInnerUsage -= it.DynamicMemoryUsage();
			cachedInnerUsage -= mapLinks[it]?.Parents?.Sum(p => p.DynamicMemoryUsage()) ?? 0 + mapLinks[it]?.Children?.Sum(p => p.DynamicMemoryUsage()) ?? 0;
			mapLinks.Remove(it);
			MapTx.Remove(it);
			nTransactionsUpdated++;
			MinerPolicyEstimator.RemoveTx(hash);
		}

		// Calculates descendants of entry that are not already in setDescendants, and adds to
		// setDescendants. Assumes entryit is already a tx in the mempool and setMemPoolChildren
		// is correct for tx and all descendants.
		// Also assumes that if an entry is in setDescendants already, then all
		// in-mempool descendants of it are already in setDescendants as well, so that we
		// can save time by not iterating over those entries.
		public void CalculateDescendants(TxMempoolEntry entryit, SetEntries setDescendants)
		{
			SetEntries stage = new SetEntries();
			if (!setDescendants.Contains(entryit))
			{
				stage.Add(entryit);
			}
			// Traverse down the children of entry, only adding children that are not
			// accounted for in setDescendants already (because those children have either
			// already been walked, or will be walked in this iteration).
			while (stage.Any())
			{
				var it = stage.First();
				setDescendants.Add(it);
				stage.Remove(it);

				var setChildren = GetMemPoolChildren(it);
				foreach (var childiter in setChildren)
				{
					if (!setDescendants.Contains(childiter))
					{
						stage.Add(childiter);
					}
				}
			}
		}

		/** For each transaction being removed, update ancestors and any direct children.
		* If updateDescendants is true, then also update in-mempool descendants'
		* ancestor state. */

		private void UpdateForRemoveFromMempool(SetEntries entriesToRemove, bool updateDescendants)
		{
			// For each entry, walk back all ancestors and decrement size associated with this
			// transaction
			var nNoLimit = long.MaxValue;

			if (updateDescendants)
			{
				// updateDescendants should be true whenever we're not recursively
				// removing a tx and all its descendants, eg when a transaction is
				// confirmed in a block.
				// Here we only update statistics and not data in mapLinks (which
				// we need to preserve until we're finished with all operations that
				// need to traverse the mempool).
				foreach (var removeIt in entriesToRemove)
				{

					SetEntries setDescendants = new SetEntries();
					CalculateDescendants(removeIt, setDescendants);
					setDescendants.Remove(removeIt); // don't update state for self
					var modifySize = -removeIt.GetTxSize();
					var modifyFee = -removeIt.ModifiedFee;
					var modifySigOps = -removeIt.SigOpCost;

					foreach (var dit in setDescendants)
						dit.UpdateAncestorState(modifySize, modifyFee, -1, modifySigOps);
				}
			}

			foreach (var entry in entriesToRemove)
			{
				SetEntries setAncestors = new SetEntries();
				string dummy = string.Empty;
				// Since this is a tx that is already in the mempool, we can call CMPA
				// with fSearchForParents = false.  If the mempool is in a consistent
				// state, then using true or false should both be correct, though false
				// should be a bit faster.
				// However, if we happen to be in the middle of processing a reorg, then
				// the mempool can be in an inconsistent state.  In this case, the set
				// of ancestors reachable via mapLinks will be the same as the set of 
				// ancestors whose packages include this transaction, because when we
				// add a new transaction to the mempool in addUnchecked(), we assume it
				// has no children, and in the case of a reorg where that assumption is
				// false, the in-mempool children aren't linked to the in-block tx's
				// until UpdateTransactionsFromBlock() is called.
				// So if we're being called during a reorg, ie before
				// UpdateTransactionsFromBlock() has been called, then mapLinks[] will
				// differ from the set of mempool parents we'd calculate by searching,
				// and it's important that we use the mapLinks[] notion of ancestor
				// transactions as the set of things to update for removal.
				CalculateMemPoolAncestors(entry, setAncestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);
				// Note that UpdateAncestorsOf severs the child links that point to
				// removeIt in the entries for the parents of removeIt.
				UpdateAncestorsOf(false, entry, setAncestors);
			}

			// After updating all the ancestor sizes, we can now sever the link between each
			// transaction being removed and any mempool children (ie, update setMemPoolParents
			// for each direct child of a transaction being removed).
			foreach (var removeIt in entriesToRemove)
			{
				UpdateChildrenForRemoval(removeIt);
			}
		}

		/** Sever link between specified transaction and direct children. */
		private void UpdateChildrenForRemoval(TxMempoolEntry it)
		{
			var setMemPoolChildren = GetMemPoolChildren(it);
			foreach (var updateIt in setMemPoolChildren)
				UpdateParent(updateIt, it, false);
		}

		/**
		* Called when a block is connected. Removes from mempool and updates the miner fee estimator.
		*/

		public void RemoveForBlock(IEnumerable<Transaction> vtx, int blockHeight)
		{
			var entries = new List<TxMempoolEntry>();
			foreach (var tx in vtx)
			{
				uint256 hash = tx.GetHash();
				var entry = this.MapTx.TryGet(hash);
				if (entry != null)
					entries.Add(entry);
			}

			// Before the txs in the new block have been removed from the mempool, update policy estimates
			MinerPolicyEstimator.ProcessBlock(blockHeight, entries);
			foreach (var tx in vtx)
			{
				uint256 hash = tx.GetHash();

				var entry = this.MapTx.TryGet(hash);
				if (entry != null)
				{
					SetEntries stage = new SetEntries();
					stage.Add(entry);
					RemoveStaged(stage, true);
				}

				RemoveConflicts(tx);
				ClearPrioritisation(tx.GetHash());
			}
			lastRollingFeeUpdate = this.TimeProvider.GetTime();
			blockSinceLastRollingFeeBump = true;
		}

		private void RemoveConflicts(Transaction tx)
		{
			// Remove transactions which depend on inputs of tx, recursively
			//LOCK(cs);
			foreach (var txInput in tx.Inputs)
			{
				var it = MapNextTx.FirstOrDefault(p => p.OutPoint == txInput.PrevOut);
				if (it != null)
				{
					var txConflict = it.Transaction;
					if (txConflict != tx)
					{
						ClearPrioritisation(txConflict.GetHash());
						RemoveRecursive(txConflict);
					}
				}
			}
		}

		private void ClearPrioritisation(uint256 hash)
		{
			//LOCK(cs);
			mapDeltas.Remove(hash);
		}

		public long DynamicMemoryUsage()
		{
			// TODO : calculate roughly the size of each element in its list

			//LOCK(cs);
			// Estimate the overhead of mapTx to be 15 pointers + an allocation, as no exact formula for boost::multi_index_contained is implemented.
			//int sizeofEntry = 10;
			//int sizeofDelta = 10;
			//int sizeofLinks = 10;
			//int sizeofNextTx = 10;
			//int sizeofHashes = 10;

			//return sizeofEntry*this.MapTx.Count +
			//       sizeofNextTx*this.mapNextTx.Count +
			//       sizeofDelta*this.mapDeltas.Count +
			//       sizeofLinks*this.mapLinks.Count +
			//       sizeofHashes*this.vTxHashes.Count +
			//       cachedInnerUsage;
			
			return this.MapTx.Values.Sum(m => m.DynamicMemoryUsage()) + cachedInnerUsage;
		}

		public void TrimToSize(long sizelimit, List<uint256> pvNoSpendsRemaining = null)
		{
			//LOCK(cs);

			int nTxnRemoved = 0;
			FeeRate maxFeeRateRemoved = new FeeRate(0);
			while (this.MapTx.Any() && this.DynamicMemoryUsage() > sizelimit)
			{
				var it = this.MapTx.DescendantScore.First();

				// We set the new mempool min fee to the feerate of the removed set, plus the
				// "minimum reasonable fee rate" (ie some value under which we consider txn
				// to have 0 fee). This way, we don't allow txn to enter mempool with feerate
				// equal to txn which were removed with no block in between.
				FeeRate removed = new FeeRate(it.ModFeesWithDescendants, (int)it.SizeWithDescendants);
				removed = new FeeRate(new Money(removed.FeePerK + minReasonableRelayFee.FeePerK));

				trackPackageRemoved(removed);
				maxFeeRateRemoved = new FeeRate(Math.Max(maxFeeRateRemoved.FeePerK, removed.FeePerK));

				SetEntries stage = new SetEntries();
				this.CalculateDescendants(it, stage);
				nTxnRemoved += stage.Count;

				List<Transaction> txn = new List<Transaction>();
				if (pvNoSpendsRemaining != null)
				{
					foreach (var setEntry in stage)
						txn.Add(setEntry.Transaction);
				}

				RemoveStaged(stage, false);
				if (pvNoSpendsRemaining != null)
				{
					foreach (var tx in txn) {
						foreach (var txin in tx.Inputs)
						{
							if (this.Exists(txin.PrevOut.Hash))
								continue;
							var iter = MapNextTx.FirstOrDefault(p => p.OutPoint == new OutPoint(txin.PrevOut.Hash, 0));
							if (iter == null || iter.OutPoint.Hash != txin.PrevOut.Hash)
								pvNoSpendsRemaining.Add(txin.PrevOut.Hash);
						}
					}
				}
			}

			if (maxFeeRateRemoved > new FeeRate(0))
				Logs.Mempool.LogInformation($"Removed {nTxnRemoved} txn, rolling minimum fee bumped to {maxFeeRateRemoved}");
		}

		/** The minimum fee to get into the mempool, which may itself not be enough
		*  for larger-sized transactions.
		*  The minReasonableRelayFee constructor arg is used to bound the time it
		*  takes the fee rate to go back down all the way to 0. When the feerate
		*  would otherwise be half of this, it is set to 0 instead.
		*/
		public FeeRate GetMinFee(long sizelimit)
		{
			//LOCK(cs);
			if (!blockSinceLastRollingFeeBump || rollingMinimumFeeRate == 0)
				return new FeeRate(new Money((int)rollingMinimumFeeRate));

			var time = this.TimeProvider.GetTime();
			if (time > lastRollingFeeUpdate + 10)
			{
				double halflife = RollingFeeHalflife;
				if (DynamicMemoryUsage() < sizelimit / 4)
					halflife /= 4;
				else if (DynamicMemoryUsage() < sizelimit / 2)
					halflife /= 2;

				rollingMinimumFeeRate = rollingMinimumFeeRate / Math.Pow(2.0, (time - lastRollingFeeUpdate) / halflife);
				lastRollingFeeUpdate = time;

				if (rollingMinimumFeeRate < (double)minReasonableRelayFee.FeePerK.Satoshi / 2)
				{
					rollingMinimumFeeRate = 0;
					return new FeeRate(0);
				}
			}

			var ret =  Math.Max(rollingMinimumFeeRate, minReasonableRelayFee.FeePerK.Satoshi);
			return new FeeRate(new Money((int)ret));
		}

		public void ApplyDeltas(uint256 hash, ref double dPriorityDelta, ref Money nFeeDelta)
		{
			//LOCK(cs);
			var delta = this.mapDeltas.TryGet(hash);
			if (delta == null)
				return;
			
			dPriorityDelta += delta.Delta;
			nFeeDelta += delta.Amount;
		}

		public static double AllowFreeThreshold()
		{
			return Money.COIN * 144 / 250;
		}

		public static bool AllowFree(double dPriority)
		{
			// Large (in bytes) low-priority (new, small-coin) transactions
			// need a fee.
			return dPriority > AllowFreeThreshold();
		}

		public void WriteFeeEstimates(BitcoinStream stream)
		{
			
		}

		public void ReadFeeEstimates(BitcoinStream stream)
		{
			
		}

		public int GetTransactionsUpdated()
		{
			return nTransactionsUpdated;
		}

		public void AddTransactionsUpdated(int n)
		{
			nTransactionsUpdated += n;
		}
	}
}
