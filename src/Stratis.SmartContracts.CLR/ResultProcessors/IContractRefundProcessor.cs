using NBitcoin;

namespace Stratis.SmartContracts.CLR.ResultProcessors
{
    /// <summary>
    /// Handles refunds after smart contract execution.
    /// </summary>
    public interface IContractRefundProcessor
    {
        /// <summary>
        /// Returns the fee and refund transactions to account for gas refunds after contract execution.
        /// </summary>
        (Money, TxOut) Process(ContractTxData contractTxData,
            ulong mempoolFee, 
            uint160 sender,
            RuntimeObserver.Gas gasConsumed,
            bool outOfGas);
    }
}
