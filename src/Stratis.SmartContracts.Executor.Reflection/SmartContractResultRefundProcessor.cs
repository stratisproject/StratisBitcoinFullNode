using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

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

        public void Process(ISmartContractExecutionResult result, SmartContractCarrier carrier, Money mempoolFee)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(mempoolFee), mempoolFee);

            result.Fee = mempoolFee;

            if (result.Exception is OutOfGasException)
            {
                this.logger.LogTrace("(-)[OUTOFGAS_EXCEPTION]");
                return;
            }

            var refund = new Money(carrier.GasCostBudget - (result.GasConsumed * carrier.CallData.GasPrice));
            this.logger.LogTrace("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(carrier.GasCostBudget), carrier.GasCostBudget, nameof(result.GasConsumed), result.GasConsumed, nameof(carrier.CallData.GasPrice), carrier.CallData.GasPrice, nameof(refund), refund);

            if (refund > 0)
            {
                result.Fee -= refund;
                result.Refunds.Add(CreateRefund(carrier.Sender, refund));
            }

            this.logger.LogTrace("(-)");
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