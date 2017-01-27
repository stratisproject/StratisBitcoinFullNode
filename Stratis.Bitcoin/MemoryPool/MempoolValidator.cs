using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolValidator
	{
		public const bool DefaultRelaypriority = true;
		public const int DefaultMaxMempoolSize = 300; // Default for -maxmempool, maximum megabytes of mempool memory usage 
		public const int DefaultMinRelayTxFee = 1000; // Default for -minrelaytxfee, minimum relay fee for transactions 
		public const int DefaultLimitfreerelay = 0;
		public const int DefaultAncestorLimit = 25; // Default for -limitancestorcount, max number of in-mempool ancestors 
		public const int DefaultAncestorSizeLimit = 101; // Default for -limitancestorsize, maximum kilobytes of tx + all in-mempool ancestors 
		public const int DefaultDescendantLimit = 25; // Default for -limitdescendantcount, max number of in-mempool descendants 
		public const int DefaultDescendantSizeLimit = 101; // Default for -limitdescendantsize, maximum kilobytes of in-mempool descendants 
		public const int DefaultMempoolExpiry = 336; // Default for -mempoolexpiry, expiration time for mempool transactions in hours 

		private readonly SchedulerPairSession mempoolScheduler;
		private readonly TxMempool.DateTimeProvider dateTimeProvider;
		private readonly NodeArgs nodeArgs;
		private readonly ConcurrentChain chain;
		private readonly CoinView coinView;
		private readonly TxMempool memPool;
		private readonly ConsensusValidator consensusValidator;

		private bool enableReplacement = true;
		private bool requireStandard = true;
		public static readonly FeeRate MinRelayTxFee = new FeeRate(DefaultMinRelayTxFee);
		private readonly FreeLimiterSection FreeLimiter;

		private class FreeLimiterSection
		{
			public double FreeCount;
			public long LastTime;
		}

		public MempoolValidator(TxMempool memPool, SchedulerPairSession mempoolScheduler,
			ConsensusValidator consensusValidator, TxMempool.DateTimeProvider dateTimeProvider, NodeArgs nodeArgs,
			ConcurrentChain chain, CoinView coinView)
		{
			this.memPool = memPool;
			this.mempoolScheduler = mempoolScheduler;
			this.consensusValidator = consensusValidator;
			this.dateTimeProvider = dateTimeProvider;
			this.nodeArgs = nodeArgs;
			this.chain = chain;
			this.coinView = coinView;

			this.requireStandard = !(nodeArgs.RegTest || nodeArgs.Testnet);
			FreeLimiter = new FreeLimiterSection();
		}

		public async Task<bool> AcceptToMemoryPoolWithTime(MemepoolValidationState state, Transaction tx)
		{
			try
			{
				List<uint256> vHashTxToUncache = new List<uint256>();
				await this.AcceptToMemoryPoolWorker(state, tx, vHashTxToUncache);
				//if (!res) {
				//    BOOST_FOREACH(const uint256& hashTx, vHashTxToUncache)
				//        pcoinsTip->Uncache(hashTx);
				//}
				return true;
			}
			catch (MempoolErrorException)
			{
				return false;
			}
			catch (ConsensusErrorException consensusError)
			{
				state.Error = new MempoolError(consensusError.ConsensusError);
				return false;
			}

			// After we've (potentially) uncached entries, ensure our coins cache is still within its size limits
			//ValidationState stateDummy;
			//FlushStateToDisk(stateDummy, FLUSH_STATE_PERIODIC);
		}

		public Task<bool> AcceptToMemoryPool(MemepoolValidationState state, Transaction tx)
		{
			state.AcceptTime = dateTimeProvider.GetTime();
			return AcceptToMemoryPoolWithTime(state, tx);
		}

		private async Task AcceptToMemoryPoolWorker(MemepoolValidationState state, Transaction tx, List<uint256> vHashTxnToUncache)
		{
			var context = new MempoolValidationContext(tx, state);

			this.PreMempoolChecks(context);

			// create the MemPoolCoinView and load relevant utxoset
			context.View = new MempoolCoinView(this.coinView, this.memPool);
			await context.View.LoadView(context.Transaction);

			// adding to the mem pool can only be done sequentially
			 // use the sequential scheduler for that.
			await this.mempoolScheduler.DoSequential(() =>
			{
				// is it already in the memory pool?
				if (this.memPool.Exists(context.TransactionHash))
					state.Fail(MempoolErrors.InPool).Throw();

				// Check for conflicts with in-memory transactions
				this.CheckConflicts(context);

				this.CheckMempoolCoinView(context);

				this.CreateMempoolEntry(context, state.AcceptTime);
				this.CheckSigOps(context);
				this.CheckFee(context);

				this.CheckRateLimit(context, state.LimitFree);

				this.CheckAncestors(context);
				this.CheckReplacment(context);
				this.CheckAllInputs(context);
			
				// Remove conflicting transactions from the mempool
				foreach (var it in context.AllConflicting)
				{
					Logging.Logs.Mempool.LogInformation($"replacing tx {it.TransactionHash} with {context.TransactionHash} for {context.ModifiedFees - context.ConflictingFees} BTC additional fees, {context.EntrySize - context.ConflictingSize} delta bytes");
				}
				this.memPool.RemoveStaged(context.AllConflicting, false);

				// This transaction should only count for fee estimation if
				// the node is not behind and it is not dependent on any other
				// transactions in the mempool
				bool validForFeeEstimation = IsCurrentForFeeEstimation() && this.memPool.HasNoInputsOf(tx);

				// Store transaction in memory
				this.memPool.AddUnchecked(context.TransactionHash, context.Entry, context.SetAncestors, validForFeeEstimation);

				// trim mempool and check if tx was trimmed
				if (!state.OverrideMempoolLimit)
				{
					LimitMempoolSize(this.nodeArgs.Mempool.MaxMempool * 1000000, this.nodeArgs.Mempool.MempoolExpiry * 60 * 60);

					if (!this.memPool.Exists(context.TransactionHash))
						state.Fail(MempoolErrors.Full).Throw();
				}
			});

			//	GetMainSignals().SyncTransaction(tx, NULL, CMainSignals::SYNC_TRANSACTION_NOT_IN_BLOCK);

		}

		// Check for conflicts with in-memory transactions
		private void CheckConflicts(MempoolValidationContext context)
		{
			context.SetConflicts = new List<uint256>();

			//LOCK(pool.cs); // protect pool.mapNextTx
			foreach (var txin in context.Transaction.Inputs)
			{
				var itConflicting = this.memPool.MapNextTx.Find(f => f.OutPoint == txin.PrevOut);
				if (itConflicting != null)
				{
					var ptxConflicting = itConflicting.Transaction;
					if (!context.SetConflicts.Contains(ptxConflicting.GetHash()))
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
						bool replacementOptOut = true;
						if (enableReplacement)
						{
							foreach (var txiner in ptxConflicting.Inputs)
							{
								if (txiner.Sequence < Sequence.Final - 1)
								{
									replacementOptOut = false;
									break;
								}
							}
						}

						if (replacementOptOut)
							context.State.Fail(MempoolErrors.Conflict).Throw();

						context.SetConflicts.Add(ptxConflicting.GetHash());
					}
				}
			}
		}

		/// <summary>
		/// Checks that are done before touching the mem pool.
		/// This checks don't need to run under the mempool scheduler 
		/// </summary>
		private void PreMempoolChecks(MempoolValidationContext context)
		{
			// state filled in by CheckTransaction
			this.consensusValidator.CheckTransaction(context.Transaction);

			// Coinbase is only valid in a block, not as a loose transaction
			if (context.Transaction.IsCoinBase)
				context.State.Fail(MempoolErrors.Coinbase).Throw();

			// TODO: IsWitnessEnabled
			//// Reject transactions with witness before segregated witness activates (override with -prematurewitness)
			//bool witnessEnabled = IsWitnessEnabled(chainActive.Tip(), Params().GetConsensus());
			//if (!GetBoolArg("-prematurewitness",false) && tx.HasWitness() && !witnessEnabled) {
			//    return state.DoS(0, false, REJECT_NONSTANDARD, "no-witness-yet", true);
			//}

			// Rather not work on nonstandard transactions (unless -testnet/-regtest)
			if (this.requireStandard)
			{
				var errors = new NBitcoin.Policy.StandardTransactionPolicy().Check(context.Transaction, null);
				if (errors.Any())
					context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, errors.First().ToString())).Throw();
			}

			// Only accept nLockTime-using transactions that can be mined in the next
			// block; we don't want our mempool filled up with transactions that can't
			// be mined yet.
			if (!CheckFinalTx(context.Transaction, ConsensusValidator.StandardLocktimeVerifyFlags))
				context.State.Fail(MempoolErrors.NonFinal).Throw();
		}

		private void CheckMempoolCoinView(MempoolValidationContext context)
		{
			Check.Assert(context.View != null);

			context.LockPoints = new LockPoints();

			// do we already have it?
			//bool fHadTxInCache = pcoinsTip->HaveCoinsInCache(hash);

			if (context.View.HaveCoins(context.TransactionHash))
			{
				//if (!fHadTxInCache)
				//	vHashTxnToUncache.push_back(hash);
				context.State.Fail(new MempoolError(MempoolErrors.RejectAlreadyKnown, "txn-already-known")).Throw();
			}

			// do all inputs exist?
			// Note that this does not check for the presence of actual outputs (see the next check for that),
			// and only helps with filling in pfMissingInputs (to determine missing vs spent).
			foreach (var txin in context.Transaction.Inputs)
			{
				//if (!pcoinsTip->HaveCoinsInCache(txin.prevout.hash))
				//	vHashTxnToUncache.push_back(txin.prevout.hash);
				if (!context.View.HaveCoins(txin.PrevOut.Hash))
				{
					context.State.MissingInputs = true;
					context.State.Throw();
					//return; // fMissingInputs and !state.IsInvalid() is used to detect this condition, don't set state.Invalid()
				}
			}

			// are the actual inputs available?
			if (!context.View.HaveInputs(context.Transaction))
				context.State.Fail(new MempoolError(MempoolErrors.RejectDuplicate, "bad-txns-inputs-spent")).Throw();
		}

		private void CheckFee(MempoolValidationContext context)
		{
			Money mempoolRejectFee = this.memPool.GetMinFee(this.nodeArgs.Mempool.MaxMempool * 1000000).GetFee(context.EntrySize);
			if (mempoolRejectFee > 0 && context.ModifiedFees < mempoolRejectFee)
			{
				context.State.Fail(new MempoolError(MempoolErrors.RejectInsufficientfee,
					$"mempool-min-fee-not-met {context.Fees} < {mempoolRejectFee}")).Throw();
			}
			else if (nodeArgs.Mempool.RelayPriority && context.ModifiedFees < MinRelayTxFee.GetFee(context.EntrySize) &&
					 !TxMempool.AllowFree(context.Entry.GetPriority(this.chain.Height + 1)))
			{
				// Require that free transactions have sufficient priority to be mined in the next block.
				context.State.Fail(new MempoolError(MempoolErrors.RejectInsufficientfee, "insufficient priority")).Throw();
			}

			if (context.State.AbsurdFee > 0 && context.Fees > context.State.AbsurdFee)
				context.State.Fail(new MempoolError(MempoolErrors.RejectHighfee, $"absurdly-high-fee {context.Fees} > {context.State.AbsurdFee} ")).Throw();
		}

		private void CheckSigOps(MempoolValidationContext context)
		{
			// Check that the transaction doesn't have an excessive number of
			// sigops, making it impossible to mine. Since the coinbase transaction
			// itself can contain sigops MAX_STANDARD_TX_SIGOPS is less than
			// MAX_BLOCK_SIGOPS; we still consider this an invalid rather than
			// merely non-standard transaction.
			if (context.SigOpsCost > ConsensusValidator.MAX_BLOCK_SIGOPS_COST)
				context.State.Fail(MempoolErrors.TooManySigops).Throw();
		}

		private void CreateMempoolEntry(MempoolValidationContext context, long acceptTime)
		{
			// TODO: borrow this code form ConsensusValidator
			// Only accept BIP68 sequence locked transactions that can be mined in the next
			// block; we don't want our mempool filled up with transactions that can't
			// be mined yet.
			// Must keep pool.cs for this unless we change CheckSequenceLocks to take a
			// CoinsViewCache instead of create its own
			if (!CheckSequenceLocks(context.Transaction, ConsensusValidator.StandardLocktimeVerifyFlags, context.LockPoints))
				context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, "non-BIP68-final")).Throw();

			// Check for non-standard pay-to-script-hash in inputs
			if (requireStandard && !this.AreInputsStandard(context.Transaction, context.View))
				context.State.Fail(MempoolErrors.NonstandardInputs).Throw();

			// TODO: Implement Witness Code
			//// Check for non-standard witness in P2WSH
			//if (tx.HasWitness && requireStandard && !IsWitnessStandard(tx, context.View))
			//	state.Fail(new MempoolError(MempoolErrors.REJECT_NONSTANDARD, "bad-witness-nonstandard")).Throw();

			context.SigOpsCost = consensusValidator.GetTransactionSigOpCost(context.Transaction, context.View.Set,
				new ConsensusFlags { ScriptFlags = ScriptVerify.Standard });

			var nValueIn = context.View.GetValueIn(context.Transaction);

			context.ValueOut = context.Transaction.TotalOut;
			context.Fees = nValueIn - context.ValueOut;
			// nModifiedFees includes any fee deltas from PrioritiseTransaction
			Money nModifiedFees = context.Fees;
			double priorityDummy = 0;
			this.memPool.ApplyDeltas(context.TransactionHash, ref priorityDummy, ref nModifiedFees);
			context.ModifiedFees = nModifiedFees;

			Money inChainInputValue = Money.Zero;
			double dPriority = context.View.GetPriority(context.Transaction, this.chain.Height, inChainInputValue);

			// Keep track of transactions that spend a coinbase, which we re-scan
			// during reorgs to ensure COINBASE_MATURITY is still met.
			bool spendsCoinbase = context.View.SpendsCoinBase(context.Transaction);

			context.Entry = new TxMempoolEntry(context.Transaction, context.Fees, acceptTime, dPriority, this.chain.Height, inChainInputValue,
				spendsCoinbase, context.SigOpsCost, context.LockPoints);
			context.EntrySize = (int)context.Entry.GetTxSize();
		}

		private void CheckReplacment(MempoolValidationContext context)
		{
			// Check if it's economically rational to mine this transaction rather
			// than the ones it replaces.
			context.ConflictingFees = 0;
			context.ConflictingSize = 0;
			context.ConflictingCount = 0;
			context.AllConflicting = new TxMempool.SetEntries();

			// If we don't hold the lock allConflicting might be incomplete; the
			// subsequent RemoveStaged() and addUnchecked() calls don't guarantee
			// mempool consistency for us.
			//LOCK(pool.cs);
			if (context.SetConflicts.Any())
			{
				FeeRate newFeeRate = new FeeRate(context.ModifiedFees, context.EntrySize);
				List<uint256> setConflictsParents = new List<uint256>();
				const int maxDescendantsToVisit = 100;
				TxMempool.SetEntries setIterConflicting = new TxMempool.SetEntries();
				foreach (var hashConflicting in context.SetConflicts)
				{
					var mi = this.memPool.MapTx.TryGet(hashConflicting);
					if (mi == null)
						continue;

					// Save these to avoid repeated lookups
					setIterConflicting.Add(mi);

					// Don't allow the replacement to reduce the feerate of the
					// mempool.
					//
					// We usually don't want to accept replacements with lower
					// feerates than what they replaced as that would lower the
					// feerate of the next block. Requiring that the feerate always
					// be increased is also an easy-to-reason about way to prevent
					// DoS attacks via replacements.
					//
					// The mining code doesn't (currently) take children into
					// account (CPFP) so we only consider the feerates of
					// transactions being directly replaced, not their indirect
					// descendants. While that does mean high feerate children are
					// ignored when deciding whether or not to replace, we do
					// require the replacement to pay more overall fees too,
					// mitigating most cases.
					FeeRate oldFeeRate = new FeeRate(mi.ModifiedFee, (int)mi.GetTxSize());
					if (newFeeRate <= oldFeeRate)
					{
						context.State.Fail(new MempoolError(MempoolErrors.RejectInsufficientfee, "insufficient-fee",
								$"rejecting replacement {context.TransactionHash}; new feerate {newFeeRate} <= old feerate {oldFeeRate}"))
							.Throw();
					}

					foreach (var txin in mi.Transaction.Inputs)
					{
						setConflictsParents.Add(txin.PrevOut.Hash);
					}

					context.ConflictingCount += mi.CountWithDescendants;
				}
				// This potentially overestimates the number of actual descendants
				// but we just want to be conservative to avoid doing too much
				// work.
				if (context.ConflictingCount <= maxDescendantsToVisit)
				{
					// If not too many to replace, then calculate the set of
					// transactions that would have to be evicted
					foreach (var it in setIterConflicting)
					{
						this.memPool.CalculateDescendants(it, context.AllConflicting);
					}
					foreach (var it in context.AllConflicting)
					{
						context.ConflictingFees += it.ModifiedFee;
						context.ConflictingSize += it.GetTxSize();
					}
				}
				else
				{
					context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, "too many potential replacements",
							$"rejecting replacement {context.TransactionHash}; too many potential replacements ({context.ConflictingCount} > {maxDescendantsToVisit})"))
						.Throw();
				}

				for (int j = 0; j < context.Transaction.Inputs.Count; j++)
				{
					// We don't want to accept replacements that require low
					// feerate junk to be mined first. Ideally we'd keep track of
					// the ancestor feerates and make the decision based on that,
					// but for now requiring all new inputs to be confirmed works.
					if (!setConflictsParents.Contains(context.Transaction.Inputs[j].PrevOut.Hash))
					{
						// Rather than check the UTXO set - potentially expensive -
						// it's cheaper to just check if the new input refers to a
						// tx that's in the mempool.
						if (this.memPool.MapTx.ContainsKey(context.Transaction.Inputs[j].PrevOut.Hash))
							context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, "replacement-adds-unconfirmed",
								$"replacement {context.TransactionHash} adds unconfirmed input, idx {j}")).Throw();
					}
				}

				// The replacement must pay greater fees than the transactions it
				// replaces - if we did the bandwidth used by those conflicting
				// transactions would not be paid for.
				if (context.ModifiedFees < context.ConflictingFees)
				{
					context.State.Fail(new MempoolError(MempoolErrors.RejectInsufficientfee, "insufficient-fee",
							$"rejecting replacement {context.TransactionHash}, less fees than conflicting txs; {context.ModifiedFees} < {context.ConflictingFees}"))
						.Throw();
				}

				// Finally in addition to paying more fees than the conflicts the
				// new transaction must pay for its own bandwidth.
				Money nDeltaFees = context.ModifiedFees - context.ConflictingFees;
				if (nDeltaFees < MinRelayTxFee.GetFee(context.EntrySize))
				{
					context.State.Fail(new MempoolError(MempoolErrors.RejectInsufficientfee, "insufficient-fee",
							$"rejecting replacement {context.TransactionHash}, not enough additional fees to relay; {nDeltaFees} < {MinRelayTxFee.GetFee(context.EntrySize)}"))
						.Throw();
				}

			}
		}

		private void CheckRateLimit(MempoolValidationContext context, bool limitFree)
		{
			// TODO: sort this logic
			return;

			// Continuously rate-limit free (really, very-low-fee) transactions
			// This mitigates 'penny-flooding' -- sending thousands of free transactions just to
			// be annoying or make others' transactions take longer to confirm.
			if (limitFree && context.ModifiedFees < MinRelayTxFee.GetFee(context.EntrySize))
			{
				var nNow = this.dateTimeProvider.GetTime();

				// Use an exponentially decaying ~10-minute window:
				this.FreeLimiter.FreeCount *= Math.Pow(1.0 - 1.0 / 600.0, (double)(nNow - this.FreeLimiter.LastTime));
				this.FreeLimiter.LastTime = nNow;
				// -limitfreerelay unit is thousand-bytes-per-minute
				// At default rate it would take over a month to fill 1GB
				if (this.FreeLimiter.FreeCount + context.EntrySize >= this.nodeArgs.Mempool.LimitFreeRelay * 10 * 1000)
					context.State.Fail(new MempoolError(MempoolErrors.RejectInsufficientfee, "rate limited free transaction")).Throw();

				Logging.Logs.Mempool.LogInformation(
					$"Rate limit dFreeCount: {this.FreeLimiter.FreeCount} => {this.FreeLimiter.FreeCount + context.EntrySize}");
				this.FreeLimiter.FreeCount += context.EntrySize;
			}
		}

		private void CheckAncestors(MempoolValidationContext context)
		{
			// Calculate in-mempool ancestors, up to a limit.
			context.SetAncestors = new TxMempool.SetEntries();
			var nLimitAncestors = nodeArgs.Mempool.LimitAncestors;
			var nLimitAncestorSize = nodeArgs.Mempool.LimitAncestorSize * 1000;
			var nLimitDescendants = nodeArgs.Mempool.LimitDescendants;
			var nLimitDescendantSize = nodeArgs.Mempool.LimitDescendantSize * 1000;
			string errString;
			if (!this.memPool.CalculateMemPoolAncestors(context.Entry, context.SetAncestors, nLimitAncestors,
				nLimitAncestorSize, nLimitDescendants, nLimitDescendantSize, out errString))
			{
				context.State.Fail(new MempoolError(MempoolErrors.RejectNonstandard, "too-long-mempool-chain", errString)).Throw();
			}

			// A transaction that spends outputs that would be replaced by it is invalid. Now
			// that we have the set of all ancestors we can detect this
			// pathological case by making sure setConflicts and setAncestors don't
			// intersect.
			foreach (var ancestorIt in context.SetAncestors)
			{
				var hashAncestor = ancestorIt.TransactionHash;
				if (context.SetConflicts.Contains(hashAncestor))
				{
					context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "bad-txns-spends-conflicting-tx",
						$"{context.TransactionHash} spends conflicting transaction {hashAncestor}")).Throw();
				}
			}
		}

		private void LimitMempoolSize(long limit, long age)
		{
			int expired = this.memPool.Expire(this.dateTimeProvider.GetTime() - age);
			if (expired != 0)
				Logging.Logs.Mempool.LogInformation($"Expired {expired} transactions from the memory pool");

			List<uint256> vNoSpendsRemaining = new List<uint256>();
			this.memPool.TrimToSize(limit, vNoSpendsRemaining);

			//foreach(var removed in vNoSpendsRemaining)
			//	pcoinsTip->Uncache(removed);
		}

		private static bool IsCurrentForFeeEstimation()
		{
			// TODO: implement method

			//AssertLockHeld(cs_main);
			//if (IsInitialBlockDownload())
			//	return false;
			//if (chainActive.Tip()->GetBlockTime() < (GetTime() - MAX_FEE_ESTIMATION_TIP_AGE))
			//	return false;
			//if (chainActive.Height() < pindexBestHeader->nHeight - 1)
			//	return false;
			return true;
		}

		private void CheckAllInputs(MempoolValidationContext context)
		{
			var scriptVerifyFlags = ScriptVerify.Standard;
			if (!this.requireStandard)
			{
				// TODO: implement -promiscuousmempoolflags
				// scriptVerifyFlags = GetArg("-promiscuousmempoolflags", scriptVerifyFlags);
			}

			// Check against previous transactions
			// This is done last to help prevent CPU exhaustion denial-of-service attacks.
			PrecomputedTransactionData txdata = new PrecomputedTransactionData(context.Transaction);
			if (!CheckInputs(context, scriptVerifyFlags, txdata))
			{
				// TODO: implement witness checks
				//// SCRIPT_VERIFY_CLEANSTACK requires SCRIPT_VERIFY_WITNESS, so we
				//// need to turn both off, and compare against just turning off CLEANSTACK
				//// to see if the failure is specifically due to witness validation.
				//if (!tx.HasWitness() && CheckInputs(tx, state, view, true, scriptVerifyFlags & ~(SCRIPT_VERIFY_WITNESS | SCRIPT_VERIFY_CLEANSTACK), true, txdata) &&
				//	!CheckInputs(tx, state, view, true, scriptVerifyFlags & ~SCRIPT_VERIFY_CLEANSTACK, true, txdata))
				//{
				//	// Only the witness is missing, so the transaction itself may be fine.
				//	state.SetCorruptionPossible();
				//}

				context.State.Fail(new MempoolError("check inputs")).Throw();
			}

			// Check again against just the consensus-critical mandatory script
			// verification flags, in case of bugs in the standard flags that cause
			// transactions to pass as valid when they're actually invalid. For
			// instance the STRICTENC flag was incorrectly allowing certain
			// CHECKSIG NOT scripts to pass, even though they were invalid.
			//
			// There is a similar check in CreateNewBlock() to prevent creating
			// invalid blocks, however allowing such transactions into the mempool
			// can be exploited as a DoS attack.
			if (!CheckInputs(context, ScriptVerify.P2SH, txdata))
			{
				context.State.Fail(new MempoolError($"CheckInputs: BUG! PLEASE REPORT THIS! ConnectInputs failed against MANDATORY but not STANDARD flags {context.TransactionHash}")).Throw();
			}
		}

		private bool CheckInputs(MempoolValidationContext context, ScriptVerify scriptVerify,
			PrecomputedTransactionData txData)
		{
			var tx = context.Transaction;
			if (!context.Transaction.IsCoinBase)
			{
				this.consensusValidator.CheckIntputs(context.Transaction, context.View.Set, this.chain.Height + 1);

				for (int iInput = 0; iInput < tx.Inputs.Count; iInput++)
				{
					var input = tx.Inputs[iInput];
					int iiIntput = iInput;
					var txout = context.View.GetOutputFor(input);

					if (this.consensusValidator.UseConsensusLib)
					{
						Script.BitcoinConsensusError error;
						return Script.VerifyScriptConsensus(txout.ScriptPubKey, tx, (uint) iiIntput, scriptVerify, out error);
					}
					else
					{
						var checker = new TransactionChecker(tx, iiIntput, txout.Value, txData);
						var ctx = new ScriptEvaluationContext();
						ctx.ScriptVerify = scriptVerify;
						return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
					}
				}
			}

			return true;
		}

		private bool CheckFinalTx(Transaction tx, Transaction.LockTimeFlags flags)
		{
			// By convention a negative value for flags indicates that the
			// current network-enforced consensus rules should be used. In
			// a future soft-fork scenario that would mean checking which
			// rules would be enforced for the next block and setting the
			// appropriate flags. At the present time no soft-forks are
			// scheduled, so no flags are set.
			flags = (Transaction.LockTimeFlags) Math.Max((int) flags, (int) Transaction.LockTimeFlags.None);

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
		private bool CheckSequenceLocks(Transaction tx, Transaction.LockTimeFlags flags, LockPoints lp = null,
			bool useExistingLockPoints = false)
		{
			//tx.CheckSequenceLocks()
			// todo:
			return true;
		}


		// Check for standard transaction types
		// @param[in] mapInputs    Map of previous transactions that have outputs we're spending
		// @return True if all inputs (scriptSigs) use only standard transaction forms
		private bool AreInputsStandard(Transaction tx, MempoolCoinView mapInputs)
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

		private bool IsWitnessStandard(Transaction tx, MempoolCoinView mapInputs)
		{
			// todo:
			return true;
		}

		public static int GetTransactionWeight(Transaction tx)
		{
			return tx.GetSerializedSize(
				       (ProtocolVersion)
				       ((uint) ProtocolVersion.PROTOCOL_VERSION | ConsensusValidator.SERIALIZE_TRANSACTION_NO_WITNESS),
				       SerializationType.Network)*(ConsensusValidator.WITNESS_SCALE_FACTOR - 1) +
			       tx.GetSerializedSize(ProtocolVersion.PROTOCOL_VERSION, SerializationType.Network);
		}

		public static int CalculateModifiedSize(int nTxSize, Transaction trx)
		{
			// In order to avoid disincentivizing cleaning up the UTXO set we don't count
			// the constant overhead for each txin and up to 110 bytes of scriptSig (which
			// is enough to cover a compressed pubkey p2sh redemption) for priority.
			// Providing any more cleanup incentive than making additional inputs free would
			// risk encouraging people to create junk outputs to redeem later.
			if (nTxSize == 0)
				nTxSize = (GetTransactionWeight(trx) + ConsensusValidator.WITNESS_SCALE_FACTOR - 1)/ ConsensusValidator.WITNESS_SCALE_FACTOR;

			foreach (var txInput in trx.Inputs)
			{
				var offset = 41U + Math.Min(110U, txInput.ScriptSig.Length);
				if (nTxSize > offset)
					nTxSize -= (int) offset;
			}
			return nTxSize;
		}

		public Task SanityCheck()
		{
			return this.mempoolScheduler.DoConcurrent(() => this.memPool.Check(this.coinView));
		}
	}
}
