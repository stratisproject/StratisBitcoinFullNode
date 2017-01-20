using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolValidator
	{
		public const int SERIALIZE_TRANSACTION_NO_WITNESS = 0x40000000;

		public const int WITNESS_SCALE_FACTOR = 4;

		private readonly SchedulerPairSession mempoolScheduler;
		private readonly TxMemPool.DateTimeProvider dateTimeProvider;
		private readonly NodeArgs nodeArgs;
		private readonly ConcurrentChain chain;
		private readonly CachedCoinView cachedCoinView;
		private readonly TxMemPool memPool;
		private readonly ConsensusValidator consensusValidator;

		private bool fEnableReplacement = true;
		private bool fRequireStandard = true;

		public MempoolValidator(TxMemPool memPool, SchedulerPairSession mempoolScheduler,
			ConsensusValidator consensusValidator, TxMemPool.DateTimeProvider dateTimeProvider, NodeArgs nodeArgs, ConcurrentChain chain, CachedCoinView cachedCoinView)
		{
			this.memPool = memPool;
			this.mempoolScheduler = mempoolScheduler;
			this.consensusValidator = consensusValidator;
			this.dateTimeProvider = dateTimeProvider;
			this.nodeArgs = nodeArgs;
			this.chain = chain;
			this.cachedCoinView = cachedCoinView;

			this.fRequireStandard = !(nodeArgs.RegTest || nodeArgs.Testnet);
		}

		public async Task<bool> AcceptToMemoryPoolWithTime(MemepoolValidationState state, Transaction tx,
			bool fLimitFree, long nAcceptTime, bool fOverrideMempoolLimit, Money nAbsurdFee)
		{
				try
				{
					List<uint256> vHashTxToUncache = new List<uint256>();
					 await this.AcceptToMemoryPoolWorker(state, tx, fLimitFree, nAcceptTime, fOverrideMempoolLimit, nAbsurdFee, vHashTxToUncache);
					//if (!res) {
					//    BOOST_FOREACH(const uint256& hashTx, vHashTxToUncache)
					//        pcoinsTip->Uncache(hashTx);
					//}
					return true;
				}
				catch (ConsensusErrorException consensusError)
				{
					state.Error = new MemepoolError(consensusError.ConsensusError);
					return false;
				}

			// After we've (potentially) uncached entries, ensure our coins cache is still within its size limits
			//ValidationState stateDummy;
			//FlushStateToDisk(stateDummy, FLUSH_STATE_PERIODIC);
		}

		public Task<bool> AcceptToMemoryPool(MemepoolValidationState state, Transaction tx, bool fLimitFree,
			bool fOverrideMempoolLimit, Money nAbsurdFee)
		{
			return AcceptToMemoryPoolWithTime(state, tx, fLimitFree, this.dateTimeProvider.GetTime(), fOverrideMempoolLimit,
				nAbsurdFee);
		}

		private void CheckConflicts(MemepoolValidationState state, Transaction tx)
		{
			// Check for conflicts with in-memory transactions
			List<uint256> setConflicts = new List<uint256>();

			//LOCK(pool.cs); // protect pool.mapNextTx
			foreach (var txin in tx.Inputs)
			{
				var itConflicting = this.memPool.MapNextTx.Find(f => f.OutPoint == txin.PrevOut);
				if (itConflicting != null)
				{
					var ptxConflicting = itConflicting.Transaction;
					if (!setConflicts.Contains(ptxConflicting.GetHash()))
					{
						// Allow opt-out of transaction replacement by setting
						// nSequence >= maxint-1 on all inputs.
						//
						// maxint-1 is picked to still allow use of nLockTime by
						// non-replaceable transactions. All inputs rather than just one
						// is for the sake of multi-party protocols, where we don't
						// want a single party to be able to disable replacement.
						//
						// The opt-out ignores descendants as anyone relying on
						// first-seen mempool behavior should be checking all
						// unconfirmed ancestors anyway; doing otherwise is hopelessly
						// insecure.
						bool fReplacementOptOut = true;
						if (fEnableReplacement)
						{
							foreach (var txiner in ptxConflicting.Inputs)

							{
								if (txiner.Sequence < Sequence.Final - 1)
								{
									fReplacementOptOut = false;
									break;
								}
							}
						}
						if (fReplacementOptOut)
							state.Fail(new MemepoolError("txn-mempool-conflict")).Throw();
						//false, REJECT_CONFLICT, "txn-mempool-conflict");

						setConflicts.Add(ptxConflicting.GetHash());
					}
				}
			}
		}

		private async Task AcceptToMemoryPoolWorker(MemepoolValidationState state, Transaction tx, bool fLimitFree,
			long nAcceptTime, bool fOverrideMempoolLimit, Money nAbsurdFee, List<uint256> vHashTxnToUncache)
		{
			// the following code is executed on the concurrent scheduler
			await this.mempoolScheduler.DoConcurrent(async () =>
			{
				uint256 hash = tx.GetHash();

				// state filled in by CheckTransaction
				this.consensusValidator.CheckTransaction(tx);

				// Coinbase is only valid in a block, not as a loose transaction
				if (tx.IsCoinBase)
					state.Fail(new MemepoolError("coinbase")).Throw(); //.DoS(100, false, REJECT_INVALID, "coinbase");

				// TODO: IsWitnessEnabled
				//// Reject transactions with witness before segregated witness activates (override with -prematurewitness)
				//bool witnessEnabled = IsWitnessEnabled(chainActive.Tip(), Params().GetConsensus());
				//if (!GetBoolArg("-prematurewitness",false) && tx.HasWitness() && !witnessEnabled) {
				//    return state.DoS(0, false, REJECT_NONSTANDARD, "no-witness-yet", true);
				//}

				// Rather not work on nonstandard transactions (unless -testnet/-regtest)
				if (this.fRequireStandard)
				{
					var errors = new NBitcoin.Policy.StandardTransactionPolicy().Check(tx, null);
					if (errors.Any())
						state.Fail(new MemepoolError(MemepoolValidationState.REJECT_NONSTANDARD, errors.First().ToString())).Throw();
				}

				// Only accept nLockTime-using transactions that can be mined in the next
				// block; we don't want our mempool filled up with transactions that can't
				// be mined yet.
				if (!CheckFinalTx(tx, ConsensusValidator.StandardLocktimeVerifyFlags))
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_NONSTANDARD, "non final transaction")).Throw();

				// is it already in the memory pool?
				if (this.memPool.Exists(hash))
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_ALREADY_KNOWN, "txn-already-in-mempool")).Throw();

				// Check for conflicts with in-memory transactions
				this.CheckConflicts(state, tx);

				MemPoolCoinView view = new MemPoolCoinView(this.cachedCoinView, this.memPool);
				await view.LoadView(tx);

				Money nValueIn = 0;
				LockPoints lp = new LockPoints();

				// do we already have it?
				//bool fHadTxInCache = pcoinsTip->HaveCoinsInCache(hash);

				if (view.HaveCoins(hash))
				{
					//if (!fHadTxInCache)
					//	vHashTxnToUncache.push_back(hash);
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_ALREADY_KNOWN, "txn-already-known")).Throw();
				}

				// do all inputs exist?
				// Note that this does not check for the presence of actual outputs (see the next check for that),
				// and only helps with filling in pfMissingInputs (to determine missing vs spent).
				foreach (var txin in tx.Inputs)
				{
					//if (!pcoinsTip->HaveCoinsInCache(txin.prevout.hash))
					//	vHashTxnToUncache.push_back(txin.prevout.hash);
					if (!view.HaveCoins(txin.PrevOut.Hash))
					{
						state.MissingInputs = true;
						return; // fMissingInputs and !state.IsInvalid() is used to detect this condition, don't set state.Invalid()
					}
				}

				// are the actual inputs available?
				if (!view.HaveInputs(tx))
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_DUPLICATE, "bad-txns-inputs-spent")).Throw();

				// Bring the best block into scope
				//view.GetBestBlock();

				nValueIn = view.GetValueIn(tx);

				// we have all inputs cached now, so switch back to dummy, so we don't need to keep lock on mempool
				//view.SetBackend(dummy);

				// Only accept BIP68 sequence locked transactions that can be mined in the next
				// block; we don't want our mempool filled up with transactions that can't
				// be mined yet.
				// Must keep pool.cs for this unless we change CheckSequenceLocks to take a
				// CoinsViewCache instead of create its own
				if (!CheckSequenceLocks(tx, ConsensusValidator.StandardLocktimeVerifyFlags, lp))
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_NONSTANDARD, "non-BIP68-final")).Throw();

				// Check for non-standard pay-to-script-hash in inputs
				if (fRequireStandard && !this.AreInputsStandard(tx, view))
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_NONSTANDARD, "bad-txns-nonstandard-inputs")).Throw();

				// Check for non-standard witness in P2WSH
				if (tx.HasWitness && fRequireStandard && !IsWitnessStandard(tx, view))
					state.Fail(new MemepoolError(MemepoolValidationState.REJECT_NONSTANDARD, "bad-witness-nonstandard")).Throw();

				var nSigOpsCost = consensusValidator.GetTransactionSigOpCost(tx, view.Set,
					new ConsensusFlags() {ScriptFlags = ScriptVerify.Standard});

				Money nValueOut = tx.TotalOut;
				Money nFees = nValueIn - nValueOut;
				// nModifiedFees includes any fee deltas from PrioritiseTransaction
				Money nModifiedFees = nFees;
				double nPriorityDummy = 0;
				this.memPool.ApplyDeltas(hash, ref nPriorityDummy, ref nModifiedFees);

				Money inChainInputValue = Money.Zero;
				double dPriority = view.GetPriority(tx, this.chain.Height, inChainInputValue);

				// Keep track of transactions that spend a coinbase, which we re-scan
				// during reorgs to ensure COINBASE_MATURITY is still met.
				bool fSpendsCoinbase = false;
				foreach (var txInput in tx.Inputs)
				{
					var coins = view.Set.AccessCoins(txInput.PrevOut.Hash);
					if (coins.IsCoinbase)
					{
						fSpendsCoinbase = true;
						break;
					}
				}
			});




			//		CTxMemPoolEntry entry(ptx, nFees, nAcceptTime, dPriority, chainActive.Height(),
			//							  inChainInputValue, fSpendsCoinbase, nSigOpsCost, lp);
			//		unsigned int nSize = entry.GetTxSize();

			//		// Check that the transaction doesn't have an excessive number of
			//		// sigops, making it impossible to mine. Since the coinbase transaction
			//		// itself can contain sigops MAX_STANDARD_TX_SIGOPS is less than
			//		// MAX_BLOCK_SIGOPS; we still consider this an invalid rather than
			//		// merely non-standard transaction.
			//		if (nSigOpsCost > MAX_STANDARD_TX_SIGOPS_COST)
			//			return state.DoS(0, false, REJECT_NONSTANDARD, "bad-txns-too-many-sigops", false,
			//				strprintf("%d", nSigOpsCost));

			//		CAmount mempoolRejectFee = pool.GetMinFee(GetArg("-maxmempool", DEFAULT_MAX_MEMPOOL_SIZE) * 1000000).GetFee(nSize);
			//		if (mempoolRejectFee > 0 && nModifiedFees < mempoolRejectFee)
			//		{
			//			return state.DoS(0, false, REJECT_INSUFFICIENTFEE, "mempool min fee not met", false, strprintf("%d < %d", nFees, mempoolRejectFee));
			//		}
			//		else if (GetBoolArg("-relaypriority", DEFAULT_RELAYPRIORITY) && nModifiedFees < ::minRelayTxFee.GetFee(nSize) && !AllowFree(entry.GetPriority(chainActive.Height() + 1)))
			//		{
			//			// Require that free transactions have sufficient priority to be mined in the next block.
			//			return state.DoS(0, false, REJECT_INSUFFICIENTFEE, "insufficient priority");
			//		}

			//		// Continuously rate-limit free (really, very-low-fee) transactions
			//		// This mitigates 'penny-flooding' -- sending thousands of free transactions just to
			//		// be annoying or make others' transactions take longer to confirm.
			//		if (fLimitFree && nModifiedFees < ::minRelayTxFee.GetFee(nSize))
			//		{
			//			static CCriticalSection csFreeLimiter;
			//			static double dFreeCount;
			//			static int64_t nLastTime;
			//			int64_t nNow = GetTime();

			//			LOCK(csFreeLimiter);

			//			// Use an exponentially decaying ~10-minute window:
			//			dFreeCount *= pow(1.0 - 1.0 / 600.0, (double)(nNow - nLastTime));
			//			nLastTime = nNow;
			//			// -limitfreerelay unit is thousand-bytes-per-minute
			//			// At default rate it would take over a month to fill 1GB
			//			if (dFreeCount + nSize >= GetArg("-limitfreerelay", DEFAULT_LIMITFREERELAY) * 10 * 1000)
			//				return state.DoS(0, false, REJECT_INSUFFICIENTFEE, "rate limited free transaction");
			//			LogPrint("mempool", "Rate limit dFreeCount: %g => %g\n", dFreeCount, dFreeCount + nSize);
			//			dFreeCount += nSize;
			//		}

			//		if (nAbsurdFee && nFees > nAbsurdFee)
			//			return state.Invalid(false,
			//				REJECT_HIGHFEE, "absurdly-high-fee",
			//				strprintf("%d > %d", nFees, nAbsurdFee));

			//		// Calculate in-mempool ancestors, up to a limit.
			//		TxMemPool.SetEntries setAncestors = new TxMemPool.SetEntries();
			//		size_t nLimitAncestors = GetArg("-limitancestorcount", DEFAULT_ANCESTOR_LIMIT);
			//		size_t nLimitAncestorSize = GetArg("-limitancestorsize", DEFAULT_ANCESTOR_SIZE_LIMIT) * 1000;
			//		size_t nLimitDescendants = GetArg("-limitdescendantcount", DEFAULT_DESCENDANT_LIMIT);
			//		size_t nLimitDescendantSize = GetArg("-limitdescendantsize", DEFAULT_DESCENDANT_SIZE_LIMIT) * 1000;
			//		std::string errString;
			//		if (!pool.CalculateMemPoolAncestors(entry, setAncestors, nLimitAncestors, nLimitAncestorSize, nLimitDescendants, nLimitDescendantSize, errString))
			//		{
			//			return state.DoS(0, false, REJECT_NONSTANDARD, "too-long-mempool-chain", false, errString);
			//		}

			//		// A transaction that spends outputs that would be replaced by it is invalid. Now
			//		// that we have the set of all ancestors we can detect this
			//		// pathological case by making sure setConflicts and setAncestors don't
			//		// intersect.
			//		BOOST_FOREACH(CTxMemPool::txiter ancestorIt, setAncestors)

			//{
			//			const uint256 &hashAncestor = ancestorIt->GetTx().GetHash();
			//			if (setConflicts.count(hashAncestor))
			//			{
			//				return state.DoS(10, false,
			//								 REJECT_INVALID, "bad-txns-spends-conflicting-tx", false,
			//								 strprintf("%s spends conflicting transaction %s",
			//										   hash.ToString(),
			//										   hashAncestor.ToString()));
			//			}
			//		}

			//		// Check if it's economically rational to mine this transaction rather
			//		// than the ones it replaces.
			//		CAmount nConflictingFees = 0;
			//		size_t nConflictingSize = 0;
			//		uint64_t nConflictingCount = 0;
			//		CTxMemPool::setEntries allConflicting;

			//		// If we don't hold the lock allConflicting might be incomplete; the
			//		// subsequent RemoveStaged() and addUnchecked() calls don't guarantee
			//		// mempool consistency for us.
			//		LOCK(pool.cs);
			//		if (setConflicts.size())
			//		{
			//			CFeeRate newFeeRate(nModifiedFees, nSize);
			//			set<uint256> setConflictsParents;
			//			const int maxDescendantsToVisit = 100;
			//			CTxMemPool::setEntries setIterConflicting;
			//			BOOST_FOREACH(const uint256 &hashConflicting, setConflicts)
			//          {
			//				CTxMemPool::txiter mi = pool.mapTx.find(hashConflicting);
			//				if (mi == pool.mapTx.end())
			//					continue;

			//				// Save these to avoid repeated lookups
			//				setIterConflicting.insert(mi);

			//				// Don't allow the replacement to reduce the feerate of the
			//				// mempool.
			//				//
			//				// We usually don't want to accept replacements with lower
			//				// feerates than what they replaced as that would lower the
			//				// feerate of the next block. Requiring that the feerate always
			//				// be increased is also an easy-to-reason about way to prevent
			//				// DoS attacks via replacements.
			//				//
			//				// The mining code doesn't (currently) take children into
			//				// account (CPFP) so we only consider the feerates of
			//				// transactions being directly replaced, not their indirect
			//				// descendants. While that does mean high feerate children are
			//				// ignored when deciding whether or not to replace, we do
			//				// require the replacement to pay more overall fees too,
			//				// mitigating most cases.
			//				CFeeRate oldFeeRate(mi->GetModifiedFee(), mi->GetTxSize());
			//				if (newFeeRate <= oldFeeRate)
			//				{
			//					return state.DoS(0, false,
			//							REJECT_INSUFFICIENTFEE, "insufficient fee", false,
			//							strprintf("rejecting replacement %s; new feerate %s <= old feerate %s",
			//								  hash.ToString(),
			//								  newFeeRate.ToString(),
			//								  oldFeeRate.ToString()));
			//				}

			//				BOOST_FOREACH(const CTxIn &txin, mi->GetTx().vin)
			//              {
			//					setConflictsParents.insert(txin.prevout.hash);
			//				}

			//				nConflictingCount += mi->GetCountWithDescendants();
			//			}
			//			// This potentially overestimates the number of actual descendants
			//			// but we just want to be conservative to avoid doing too much
			//			// work.
			//			if (nConflictingCount <= maxDescendantsToVisit)
			//			{
			//				// If not too many to replace, then calculate the set of
			//				// transactions that would have to be evicted
			//				BOOST_FOREACH(CTxMemPool::txiter it, setIterConflicting) {
			//					pool.CalculateDescendants(it, allConflicting);
			//				}
			//				BOOST_FOREACH(CTxMemPool::txiter it, allConflicting) {
			//					nConflictingFees += it->GetModifiedFee();
			//					nConflictingSize += it->GetTxSize();
			//				}
			//			}
			//			else
			//			{
			//				return state.DoS(0, false,
			//						REJECT_NONSTANDARD, "too many potential replacements", false,
			//						strprintf("rejecting replacement %s; too many potential replacements (%d > %d)\n",
			//							hash.ToString(),
			//							nConflictingCount,
			//							maxDescendantsToVisit));
			//			}

			//			for (unsigned int j = 0; j < tx.vin.size(); j++)
			//			{
			//				// We don't want to accept replacements that require low
			//				// feerate junk to be mined first. Ideally we'd keep track of
			//				// the ancestor feerates and make the decision based on that,
			//				// but for now requiring all new inputs to be confirmed works.
			//				if (!setConflictsParents.count(tx.vin[j].prevout.hash))
			//				{
			//					// Rather than check the UTXO set - potentially expensive -
			//					// it's cheaper to just check if the new input refers to a
			//					// tx that's in the mempool.
			//					if (pool.mapTx.find(tx.vin[j].prevout.hash) != pool.mapTx.end())
			//						return state.DoS(0, false,
			//										 REJECT_NONSTANDARD, "replacement-adds-unconfirmed", false,
			//										 strprintf("replacement %s adds unconfirmed input, idx %d",
			//												  hash.ToString(), j));
			//				}
			//			}

			//			// The replacement must pay greater fees than the transactions it
			//			// replaces - if we did the bandwidth used by those conflicting
			//			// transactions would not be paid for.
			//			if (nModifiedFees < nConflictingFees)
			//			{
			//				return state.DoS(0, false,
			//								 REJECT_INSUFFICIENTFEE, "insufficient fee", false,
			//								 strprintf("rejecting replacement %s, less fees than conflicting txs; %s < %s",
			//										  hash.ToString(), FormatMoney(nModifiedFees), FormatMoney(nConflictingFees)));
			//			}

			//			// Finally in addition to paying more fees than the conflicts the
			//			// new transaction must pay for its own bandwidth.
			//			CAmount nDeltaFees = nModifiedFees - nConflictingFees;
			//			if (nDeltaFees < ::minRelayTxFee.GetFee(nSize))
			//			{
			//				return state.DoS(0, false,
			//						REJECT_INSUFFICIENTFEE, "insufficient fee", false,
			//						strprintf("rejecting replacement %s, not enough additional fees to relay; %s < %s",
			//							  hash.ToString(),
			//							  FormatMoney(nDeltaFees),
			//							  FormatMoney(::minRelayTxFee.GetFee(nSize))));
			//			}
			//		}

			//		unsigned int scriptVerifyFlags = STANDARD_SCRIPT_VERIFY_FLAGS;
			//		if (!Params().RequireStandard())
			//		{
			//			scriptVerifyFlags = GetArg("-promiscuousmempoolflags", scriptVerifyFlags);
			//		}

			//		// Check against previous transactions
			//		// This is done last to help prevent CPU exhaustion denial-of-service attacks.
			//		PrecomputedTransactionData txdata(tx);
			//		if (!CheckInputs(tx, state, view, true, scriptVerifyFlags, true, txdata))
			//		{
			//			// SCRIPT_VERIFY_CLEANSTACK requires SCRIPT_VERIFY_WITNESS, so we
			//			// need to turn both off, and compare against just turning off CLEANSTACK
			//			// to see if the failure is specifically due to witness validation.
			//			if (!tx.HasWitness() && CheckInputs(tx, state, view, true, scriptVerifyFlags & ~(SCRIPT_VERIFY_WITNESS | SCRIPT_VERIFY_CLEANSTACK), true, txdata) &&
			//				!CheckInputs(tx, state, view, true, scriptVerifyFlags & ~SCRIPT_VERIFY_CLEANSTACK, true, txdata))
			//			{
			//				// Only the witness is missing, so the transaction itself may be fine.
			//				state.SetCorruptionPossible();
			//			}
			//			return false;
			//		}

			//		// Check again against just the consensus-critical mandatory script
			//		// verification flags, in case of bugs in the standard flags that cause
			//		// transactions to pass as valid when they're actually invalid. For
			//		// instance the STRICTENC flag was incorrectly allowing certain
			//		// CHECKSIG NOT scripts to pass, even though they were invalid.
			//		//
			//		// There is a similar check in CreateNewBlock() to prevent creating
			//		// invalid blocks, however allowing such transactions into the mempool
			//		// can be exploited as a DoS attack.
			//		if (!CheckInputs(tx, state, view, true, MANDATORY_SCRIPT_VERIFY_FLAGS, true, txdata))
			//		{
			//			return error("%s: BUG! PLEASE REPORT THIS! ConnectInputs failed against MANDATORY but not STANDARD flags %s, %s",
			//				__func__, hash.ToString(), FormatStateMessage(state));
			//		}

			//		// Remove conflicting transactions from the mempool
			//		BOOST_FOREACH(const CTxMemPool::txiter it, allConflicting)
			//      {
			//			LogPrint("mempool", "replacing tx %s with %s for %s BTC additional fees, %d delta bytes\n",
			//					it->GetTx().GetHash().ToString(),
			//					hash.ToString(),
			//					FormatMoney(nModifiedFees - nConflictingFees),
			//					(int)nSize - (int)nConflictingSize);
			//		}
			//		pool.RemoveStaged(allConflicting, false);

			//		// This transaction should only count for fee estimation if
			//		// the node is not behind and it is not dependent on any other
			//		// transactions in the mempool
			//		bool validForFeeEstimation = IsCurrentForFeeEstimation() && pool.HasNoInputsOf(tx);

			//		// Store transaction in memory
			//		pool.addUnchecked(hash, entry, setAncestors, validForFeeEstimation);

			//		// trim mempool and check if tx was trimmed
			//		if (!fOverrideMempoolLimit)
			//		{
			//			LimitMempoolSize(pool, GetArg("-maxmempool", DEFAULT_MAX_MEMPOOL_SIZE) * 1000000, GetArg("-mempoolexpiry", DEFAULT_MEMPOOL_EXPIRY) * 60 * 60);
			//			if (!pool.exists(hash))
			//				return state.DoS(0, false, REJECT_INSUFFICIENTFEE, "mempool full");
			//		}
			//	}

			//	GetMainSignals().SyncTransaction(tx, NULL, CMainSignals::SYNC_TRANSACTION_NOT_IN_BLOCK);

		}

		private bool CheckFinalTx(Transaction tx, Transaction.LockTimeFlags flags)
		{
			// By convention a negative value for flags indicates that the
			// current network-enforced consensus rules should be used. In
			// a future soft-fork scenario that would mean checking which
			// rules would be enforced for the next block and setting the
			// appropriate flags. At the present time no soft-forks are
			// scheduled, so no flags are set.
			flags = (Transaction.LockTimeFlags)Math.Max((int)flags, (int)Transaction.LockTimeFlags.None);

			// CheckFinalTx() uses chainActive.Height()+1 to evaluate
			// nLockTime because when IsFinalTx() is called within
			// CBlock::AcceptBlock(), the height of the block *being*
			// evaluated is what is used. Thus if we want to know if a
			// transaction can be part of the *next* block, we need to call
			// IsFinalTx() with one more than chainActive.Height().
			int nBlockHeight = this.chain.Height + 1;

			// BIP113 will require that time-locked transactions have nLockTime set to
			// less than the median time of the previous block they're contained in.
			// When the next block is created its previous block will be the current
			// chain tip, so we use that to calculate the median time passed to
			// IsFinalTx() if LOCKTIME_MEDIAN_TIME_PAST is set.
			var nBlockTime = flags.HasFlag(ConsensusValidator.StandardLocktimeVerifyFlags)
				? this.chain.Tip.Header.BlockTime
				: DateTimeOffset.FromUnixTimeMilliseconds(this.dateTimeProvider.GetTime());

			return tx.IsFinal(nBlockTime, nBlockHeight);
		}

		
		// Check if transaction will be BIP 68 final in the next block to be created.
		// Simulates calling SequenceLocks() with data from the tip of the current active chain.
		// Optionally stores in LockPoints the resulting height and time calculated and the hash
		// of the block needed for calculation or skips the calculation and uses the LockPoints
		// passed in for evaluation.
		// The LockPoints should not be considered valid if CheckSequenceLocks returns false.
		// See consensus/consensus.h for flag definitions.
		private bool CheckSequenceLocks(Transaction tx, Transaction.LockTimeFlags flags, LockPoints lp = null, bool useExistingLockPoints = false)
		{
			//tx.CheckSequenceLocks()
			// todo:
			return true;
		}

		
	  // Check for standard transaction types
	  // @param[in] mapInputs    Map of previous transactions that have outputs we're spending
	  // @return True if all inputs (scriptSigs) use only standard transaction forms
		private bool AreInputsStandard(Transaction tx, MemPoolCoinView mapInputs)
		{
			if (tx.IsCoinBase)
				return true; // Coinbases don't use vin normally

			foreach (TxIn txin in tx.Inputs)
			{
				var prev = mapInputs.GetOutputFor(txin);
				var template = StandardScripts.GetTemplateFromScriptPubKey(prev.ScriptPubKey);
				if (template == null)
					return false;

				if (template.Type == TxOutType.TX_SCRIPTHASH)
				{
					if (prev.ScriptPubKey.GetSigOpCount(true) > 15) //MAX_P2SH_SIGOPS
						return false;
				}
			}

			return true;
		}

		private bool IsWitnessStandard(Transaction tx, MemPoolCoinView mapInputs)
		{
			// todo:
			return true;
		}

		public static int GetTransactionWeight(Transaction tx)
		{
			return tx.GetSerializedSize((ProtocolVersion) ((uint) ProtocolVersion.PROTOCOL_VERSION | MempoolValidator.SERIALIZE_TRANSACTION_NO_WITNESS),
					SerializationType.Network)* (WITNESS_SCALE_FACTOR - 1) + tx.GetSerializedSize(ProtocolVersion.PROTOCOL_VERSION, SerializationType.Network);
		}

		public static int CalculateModifiedSize(int nTxSize, Transaction trx)
		{
			// In order to avoid disincentivizing cleaning up the UTXO set we don't count
			// the constant overhead for each txin and up to 110 bytes of scriptSig (which
			// is enough to cover a compressed pubkey p2sh redemption) for priority.
			// Providing any more cleanup incentive than making additional inputs free would
			// risk encouraging people to create junk outputs to redeem later.
			if (nTxSize == 0)
				nTxSize = (GetTransactionWeight(trx) + WITNESS_SCALE_FACTOR - 1)/WITNESS_SCALE_FACTOR;

			foreach (var txInput in trx.Inputs)
			{
				var offset = 41U + Math.Min(110U, txInput.ScriptSig.Length);
				if (nTxSize > offset)
					nTxSize -= (int) offset;
			}
			return nTxSize;
		}
	}
}
