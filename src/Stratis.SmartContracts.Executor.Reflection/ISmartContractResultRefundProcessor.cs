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
        /// Alters an ISmartContractExecutionResult to account for gas refunds after contract execution.
        /// </summary>
        void Process(ISmartContractExecutionResult result, SmartContractCarrier carrier, Money mempoolFee);
    }
}
