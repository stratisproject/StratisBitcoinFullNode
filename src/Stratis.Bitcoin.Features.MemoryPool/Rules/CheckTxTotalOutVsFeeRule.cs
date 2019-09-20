using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
    /// <summary>
    /// This rule ensures that the transaction's total out is at least equal to the network's minimum relay fee.
    /// </summary>
    public sealed class CheckTxTotalOutVsFeeRule : MempoolRule
    {
        public CheckTxTotalOutVsFeeRule(
            Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        /// <inheritdoc />
        public override void CheckTransaction(MempoolValidationContext context)
        {
            if (context.ValueOut < context.MinRelayTxFee.GetFee(context.Transaction))
            {
                this.logger.LogTrace("(-)[TX_TOTALOUT_LESS_THAN_MINRELAY_FEE]");
                context.State.Fail(MempoolErrors.TxTotalOutLessThanMinRelayFee, $" {context.ValueOut} < {context.Fees}").Throw();
            }
        }
    }
}