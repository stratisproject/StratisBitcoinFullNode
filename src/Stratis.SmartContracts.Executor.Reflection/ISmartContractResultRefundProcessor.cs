using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Handles refunds after smart contract execution.
    /// </summary>
    public interface ISmartContractResultRefundProcessor
    {
        /// <summary>
        /// Returns the fee and refund transactions to account for gas refunds after contract execution.
        /// </summary>
        (Money, List<TxOut>) Process(ContractTxData contractTxData,
            ulong mempoolFee, uint160 sender,
            Gas gasConsumed,
            Exception exception);
    }
}
