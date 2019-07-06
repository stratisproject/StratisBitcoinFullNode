using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// Validates the transaction fee is valid.
    /// Checks whether the fee meets minimum fee, free transactions have sufficient priority,
    /// and absurdly high fees.
    /// </summary>
    public class CheckFeeMempoolRule : IMempoolRule
    {
        public void CheckTransaction(MempoolRuleContext ruleContext, MempoolValidationContext context)
        {
            Money mempoolRejectFee = ruleContext.Mempool.GetMinFee(ruleContext.Settings.MaxMempool * 1000000).GetFee(context.EntrySize);
            if (mempoolRejectFee > 0 && context.ModifiedFees < mempoolRejectFee)
            {
                ruleContext.Logger.LogTrace("(-)[FAIL_MIN_FEE_NOT_MET]");
                context.State.Fail(MempoolErrors.MinFeeNotMet, $" {context.Fees} < {mempoolRejectFee}").Throw();
            }
            else if (ruleContext.Settings.RelayPriority && context.ModifiedFees < ruleContext.MinRelayTxFee.GetFee(context.EntrySize) &&
                     !TxMempool.AllowFree(context.Entry.GetPriority(ruleContext.ChainIndexer.Height + 1)))
            {
                ruleContext.Logger.LogTrace("(-)[FAIL_INSUFFICIENT_PRIORITY]");
                // Require that free transactions have sufficient priority to be mined in the next block.
                context.State.Fail(MempoolErrors.InsufficientPriority).Throw();
            }

            if (context.State.AbsurdFee > 0 && context.Fees > context.State.AbsurdFee)
            {
                ruleContext.Logger.LogTrace("(-)[INVALID_ABSURD_FEE]");
                context.State.Invalid(MempoolErrors.AbsurdlyHighFee, $"{context.Fees} > {context.State.AbsurdFee}").Throw();
            }
        }
    }
}