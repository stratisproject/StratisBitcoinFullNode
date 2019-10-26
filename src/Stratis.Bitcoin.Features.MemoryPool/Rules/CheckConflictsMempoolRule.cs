using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Check for conflicts with in-memory transactions.
    /// If a conflict is found it is added to the validation context.
    /// </summary>
    public class CheckConflictsMempoolRule : MempoolRule
    {
        public CheckConflictsMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            context.SetConflicts = new List<uint256>();
            foreach (TxIn txin in context.Transaction.Inputs)
            {
                TxMempool.NextTxPair itConflicting = this.mempool.MapNextTx.Find(f => f.OutPoint == txin.PrevOut);
                if (itConflicting != null)
                {
                    Transaction ptxConflicting = itConflicting.Transaction;
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
                        if (this.settings.EnableReplacement)
                        {
                            foreach (TxIn txiner in ptxConflicting.Inputs)
                            {
                                if (txiner.Sequence < Sequence.Final - 1)
                                {
                                    replacementOptOut = false;
                                    break;
                                }
                            }
                        }

                        if (replacementOptOut)
                        {
                            this.logger.LogTrace("New transaction '{0}' and existing mempool transaction '{1}' both consume the same PrevOut: '{2}-{3}'", context.Transaction.GetHash(), ptxConflicting.GetHash(), txin.PrevOut.Hash, txin.PrevOut.N);
                            this.logger.LogTrace("New transaction = {0}", context.Transaction.ToString(this.network, RawFormat.BlockExplorer));
                            this.logger.LogTrace("Old transaction = {0}", ptxConflicting.ToString(this.network, RawFormat.BlockExplorer));
                            this.logger.LogTrace("(-)[INVALID_CONFLICT]");
                            context.State.Invalid(MempoolErrors.Conflict).Throw();
                        }

                        context.SetConflicts.Add(ptxConflicting.GetHash());
                    }
                }
            }
        }
    }
}