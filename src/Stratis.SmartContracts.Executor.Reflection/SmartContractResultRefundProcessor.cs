using System;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// This class processes the result from the <see cref="SmartContractExecutor"/> by calculating the fee
    /// and if applicable ,adding any refunds due.
    /// </summary>
    public sealed class SmartContractResultRefundProcessor : ISmartContractResultRefundProcessor
    {
        private readonly ILogger logger;

        public SmartContractResultRefundProcessor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public (Money, TxOut) Process(ContractTxData contractTxData,
            ulong mempoolFee, uint160 sender,
            Gas gasConsumed,
            bool outOfGas)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(mempoolFee), mempoolFee);

            Money fee = mempoolFee;

            if (outOfGas)
            {
                this.logger.LogTrace("(-)[OUTOFGAS_EXCEPTION]");
                return (fee, null);
            }

            var refund = new Money(contractTxData.GasCostBudget - (gasConsumed * contractTxData.GasPrice));
            this.logger.LogTrace("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(contractTxData.GasCostBudget), contractTxData.GasCostBudget, nameof(gasConsumed), gasConsumed, nameof(contractTxData.GasPrice), contractTxData.GasPrice, nameof(refund), refund);

            TxOut ret = null;

            if (refund > 0)
            {
                fee -= refund;
                ret = CreateRefund(sender, refund);
            }

            this.logger.LogTrace("(-)");

            return (fee, ret);
        }

        /// <summary>
        /// Create the script to send the relevant funds back to the user.
        /// TODO: Multiple refunds to same user should be consolidated to 1 TxOut to save space
        /// </summary>
        private TxOut CreateRefund(uint160 senderAddress, Money refund)
        {
            this.logger.LogTrace("(){0}:{1},{2}:{3}", nameof(senderAddress), senderAddress, nameof(refund), refund);

            Script senderScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(senderAddress));

            this.logger.LogTrace("(-)");

            return new TxOut(refund, senderScript);
        }
    }
}