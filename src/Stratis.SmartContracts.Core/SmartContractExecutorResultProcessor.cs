using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// This class processes the result from the <see cref="SmartContractExecutor"/> by calculating the fee
    /// and if applicable ,adding any refunds due.
    /// </summary>
    public sealed class SmartContractExecutorResultProcessor
    {
        private readonly ISmartContractExecutionResult executionResult;
        private readonly ILogger logger;

        public SmartContractExecutorResultProcessor(ISmartContractExecutionResult executionResult, ILoggerFactory loggerFactory)
        {
            this.executionResult = executionResult;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public void Process(SmartContractCarrier carrier, Money mempoolFee)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(mempoolFee), mempoolFee);

            this.executionResult.Fee = mempoolFee;

            if (this.executionResult.Exception is OutOfGasException)
            {
                this.logger.LogTrace("(-)[OUTOFGAS_EXCEPTION]");
                return;
            }

            var refund = new Money(carrier.GasCostBudget - (this.executionResult.GasConsumed * carrier.GasPrice));
            this.logger.LogTrace("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(carrier.GasCostBudget), carrier.GasCostBudget, nameof(this.executionResult.GasConsumed), this.executionResult.GasConsumed, nameof(carrier.GasPrice), carrier.GasPrice, nameof(refund), refund);

            if (refund > 0)
            {
                this.executionResult.Fee -= refund;
                this.executionResult.Refunds.Add(CreateRefund(carrier.Sender, refund));
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