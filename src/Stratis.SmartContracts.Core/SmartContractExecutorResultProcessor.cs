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

        public SmartContractExecutorResultProcessor(ISmartContractExecutionResult executionResult)
        {
            this.executionResult = executionResult;
        }

        public void Process(SmartContractCarrier carrier, Money mempoolFee)
        {
            this.executionResult.Fee = mempoolFee;

            if (this.executionResult.Exception is OutOfGasException)
                return;

            var refund = new Money(carrier.GasCostBudget - (this.executionResult.GasConsumed * carrier.GasPrice));
            if (refund > 0)
            {
                this.executionResult.Fee -= refund;
                this.executionResult.Refunds.Add(CreateRefund(carrier.Sender, refund));
            }
        }

        /// <summary>
        /// Create the script to send the relevant funds back to the user.
        /// TODO: Multiple refunds to same user should be consolidated to 1 TxOut to save space
        /// </summary>
        private TxOut CreateRefund(uint160 senderAddress, Money refund)
        {
            Script senderScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(senderAddress));
            return new TxOut(refund, senderScript);
        }
    }
}