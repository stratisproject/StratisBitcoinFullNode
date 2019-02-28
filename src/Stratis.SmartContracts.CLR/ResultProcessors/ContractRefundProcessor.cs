using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.SmartContracts.CLR.ResultProcessors
{
    /// <summary>
    /// This class processes the result from the <see cref="SmartContractExecutor"/> by calculating the fee
    /// and if applicable ,adding any refunds due.
    /// </summary>
    public sealed class ContractRefundProcessor : IContractRefundProcessor
    {
        private readonly ILogger logger;

        public ContractRefundProcessor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public (Money, TxOut) Process(ContractTxData contractTxData,
            ulong mempoolFee,
            uint160 sender,
            RuntimeObserver.Gas gasConsumed,
            bool outOfGas)
        {

            Money fee = mempoolFee;

            if (outOfGas)
            {
                this.logger.LogTrace("(-)[OUTOFGAS_EXCEPTION]");
                return (fee, null);
            }

            var refund = new Money(contractTxData.GasCostBudget - (gasConsumed * contractTxData.GasPrice));

            TxOut ret = null;

            if (refund > 0)
            {
                fee -= refund;
                ret = this.CreateRefund(sender, refund);
            }

            return (fee, ret);
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