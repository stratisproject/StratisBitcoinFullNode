using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool.Rules
{
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

        public override void CheckTransaction(MempoolValidationContext context)
        {
            if (context.ValueOut < context.Fees)
            {
                this.logger.LogTrace("(-)[TX_TOTALOUT_LESS_THAN_FEES]");
                context.State.Fail(MempoolErrors.TxTotalOutLessThanFee, $" {context.ValueOut} < {context.Fees}").Throw();
            }
        }
    }
}