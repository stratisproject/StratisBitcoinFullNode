using System;
using System.Collections.Generic;
using NBitcoin;

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
        (Money, List<TxOut>) Process(SmartContractCarrier carrier, Money mempoolFee,
            Gas gasConsumed,
            Exception exception);
    }
}
