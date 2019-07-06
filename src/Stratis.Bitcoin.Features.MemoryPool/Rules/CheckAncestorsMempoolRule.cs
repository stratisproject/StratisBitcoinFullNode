using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Validates the ancestors of a memory pool entry.
    /// Checks that the number of ancestors isn't too large.
    /// Checks for a transaction that spends outputs that would be replaced by it.
    /// </summary>
    public class CheckAncestorsMempoolRule : IMempoolRule
    {
        public void CheckTransaction(MempoolRuleContext ruleContext, MempoolValidationContext context)
        {
            // Calculate in-mempool ancestors, up to a limit.
            context.SetAncestors = new TxMempool.SetEntries();
            int nLimitAncestors = ruleContext.Settings.LimitAncestors;
            int nLimitAncestorSize = ruleContext.Settings.LimitAncestorSize * 1000;
            int nLimitDescendants = ruleContext.Settings.LimitDescendants;
            int nLimitDescendantSize = ruleContext.Settings.LimitDescendantSize * 1000;

            if (!ruleContext.Mempool.CalculateMemPoolAncestors(context.Entry, context.SetAncestors, nLimitAncestors,
                nLimitAncestorSize, nLimitDescendants, nLimitDescendantSize, out string errString))
            {
                ruleContext.Logger.LogTrace("(-)FAIL_CHAIN_TOO_LONG]");
                context.State.Fail(MempoolErrors.TooLongMempoolChain, errString).Throw();
            }

            // A transaction, that spends outputs that would be replaced by it, is invalid. Now
            // that we have the set of all ancestors we can detect this
            // pathological case by making sure setConflicts and setAncestors don't
            // intersect.
            foreach (TxMempoolEntry ancestorIt in context.SetAncestors)
            {
                uint256 hashAncestor = ancestorIt.TransactionHash;
                if (context.SetConflicts.Contains(hashAncestor))
                {
                    ruleContext.Logger.LogTrace("(-)[FAIL_BAD_TX_SPENDS_CONFLICTING]");
                    context.State.Fail(MempoolErrors.BadTxnsSpendsConflictingTx,
                        $"{context.TransactionHash} spends conflicting transaction {hashAncestor}").Throw();
                }
            }
        }
    }
}