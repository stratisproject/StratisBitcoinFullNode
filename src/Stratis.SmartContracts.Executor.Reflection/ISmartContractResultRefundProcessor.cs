using NBitcoin;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractResultRefundProcessor
    {
        void Process(ISmartContractExecutionResult result, SmartContractCarrier carrier, Money mempoolFee);
    }
}
