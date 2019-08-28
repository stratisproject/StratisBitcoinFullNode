using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Check if transaction can replace others.
    /// Only transactions that increase fees over previous transactions are accepted.
    /// There is a restriction on the maximum number of transactions that would be replaced.
    /// The new transaction must have all inputs confirmed.
    /// The new transaction must have sufficient fees to pay for it's bandwidth.
    /// </summary>
    public class CheckReplacementMempoolRule : MempoolRule
    {
        public CheckReplacementMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
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
                var newFeeRate = new FeeRate(context.ModifiedFees, context.EntrySize);
                var setConflictsParents = new List<uint256>();
                const int MaxDescendantsToVisit = 100;
                var setIterConflicting = new TxMempool.SetEntries();
                foreach (uint256 hashConflicting in context.SetConflicts)
                {
                    TxMempoolEntry mi = this.mempool.MapTx.TryGet(hashConflicting);
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
                    var oldFeeRate = new FeeRate(mi.ModifiedFee, (int)mi.GetTxSize());
                    if (newFeeRate <= oldFeeRate)
                    {
                        this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_FEE]");
                        context.State.Fail(MempoolErrors.InsufficientFee,
                            $"rejecting replacement {context.TransactionHash}; new feerate {newFeeRate} <= old feerate {oldFeeRate}").Throw();
                    }

                    foreach (TxIn txin in mi.Transaction.Inputs)
                    {
                        setConflictsParents.Add(txin.PrevOut.Hash);
                    }

                    context.ConflictingCount += mi.CountWithDescendants;
                }
                // This potentially overestimates the number of actual descendants
                // but we just want to be conservative to avoid doing too much
                // work.
                if (context.ConflictingCount <= MaxDescendantsToVisit)
                {
                    // If not too many to replace, then calculate the set of
                    // transactions that would have to be evicted
                    foreach (TxMempoolEntry it in setIterConflicting)
                    {
                        this.mempool.CalculateDescendants(it, context.AllConflicting);
                    }
                    foreach (TxMempoolEntry it in context.AllConflicting)
                    {
                        context.ConflictingFees += it.ModifiedFee;
                        context.ConflictingSize += it.GetTxSize();
                    }
                }
                else
                {
                    this.logger.LogTrace("(-)[FAIL_TOO_MANY_POTENTIAL_REPLACEMENTS]");
                    context.State.Fail(MempoolErrors.TooManyPotentialReplacements,
                            $"rejecting replacement {context.TransactionHash}; too many potential replacements ({context.ConflictingCount} > {MaxDescendantsToVisit})").Throw();
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
                        if (this.mempool.MapTx.ContainsKey(context.Transaction.Inputs[j].PrevOut.Hash))
                        {
                            this.logger.LogTrace("(-)[FAIL_REPLACEMENT_ADDS_UNCONFIRMED]");
                            context.State.Fail(MempoolErrors.ReplacementAddsUnconfirmed,
                                $"replacement {context.TransactionHash} adds unconfirmed input, idx {j}").Throw();
                        }
                    }
                }

                // The replacement must pay greater fees than the transactions it
                // replaces - if we did the bandwidth used by those conflicting
                // transactions would not be paid for.
                if (context.ModifiedFees < context.ConflictingFees)
                {
                    this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_FEE]");
                    context.State.Fail(MempoolErrors.Insufficientfee,
                            $"rejecting replacement {context.TransactionHash}, less fees than conflicting txs; {context.ModifiedFees} < {context.ConflictingFees}").Throw();
                }

                // Finally in addition to paying more fees than the conflicts the
                // new transaction must pay for its own bandwidth.
                Money nDeltaFees = context.ModifiedFees - context.ConflictingFees;
                if (nDeltaFees < context.MinRelayTxFee.GetFee(context.EntrySize))
                {
                    this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_DELTA_FEE]");
                    context.State.Fail(MempoolErrors.Insufficientfee,
                            $"rejecting replacement {context.TransactionHash}, not enough additional fees to relay; {nDeltaFees} < {context.MinRelayTxFee.GetFee(context.EntrySize)}").Throw();
                }
            }
        }
    }
}