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
    public class CheckFeeMempoolRule : MempoolRule
    {
        public CheckFeeMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            Money mempoolRejectFee = this.mempool.GetMinFee(this.settings.MaxMempool * 1000000).GetFee(context.EntrySize);
            if (mempoolRejectFee > 0 && context.ModifiedFees < mempoolRejectFee)
            {
                this.logger.LogTrace("(-)[FAIL_MIN_FEE_NOT_MET]");
                context.State.Fail(MempoolErrors.MinFeeNotMet, $" {context.Fees} < {mempoolRejectFee}").Throw();
            }
            else if (this.settings.RelayPriority && context.ModifiedFees < context.MinRelayTxFee.GetFee(context.EntrySize) &&
                     !TxMempool.AllowFree(context.Entry.GetPriority(this.chainIndexer.Height + 1)))
            {
                this.logger.LogTrace("(-)[FAIL_INSUFFICIENT_PRIORITY]");
                // Require that free transactions have sufficient priority to be mined in the next block.
                context.State.Fail(MempoolErrors.InsufficientPriority).Throw();
            }

            if (context.State.AbsurdFee > 0 && context.Fees > context.State.AbsurdFee)
            {
                this.logger.LogTrace("(-)[INVALID_ABSURD_FEE]");
                context.State.Invalid(MempoolErrors.AbsurdlyHighFee, $"{context.Fees} > {context.State.AbsurdFee}").Throw();
            }
        }
    }
}