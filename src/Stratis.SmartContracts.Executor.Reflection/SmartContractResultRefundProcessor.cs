using System;
using System.Collections.Generic;
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

        public (Money, List<TxOut>) Process(SmartContractCarrier carrier, Money mempoolFee,
            Gas gasConsumed,
            Exception exception)
        {
            this.logger.LogTrace("(){0}:{1}", nameof(mempoolFee), mempoolFee);

            Money fee = mempoolFee;

            var refunds = new List<TxOut>();

            if (exception is OutOfGasException)
            {
                this.logger.LogTrace("(-)[OUTOFGAS_EXCEPTION]");
                return (fee, refunds);
            }

            var refund = new Money(carrier.GasCostBudget - (gasConsumed * carrier.CallData.GasPrice));
            this.logger.LogTrace("{0}:{1},{2}:{3},{4}:{5},{6}:{7}", nameof(carrier.GasCostBudget), carrier.GasCostBudget, nameof(gasConsumed), gasConsumed, nameof(carrier.CallData.GasPrice), carrier.CallData.GasPrice, nameof(refund), refund);

            if (refund > 0)
            {
                fee -= refund;
                refunds.Add(CreateRefund(carrier.Sender, refund));
            }

            this.logger.LogTrace("(-)");

            return (fee, refunds);
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