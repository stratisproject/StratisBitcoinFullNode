using NBitcoin;
using Stratis.SmartContracts.Core.Backend;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractExecutor
    {
        ISmartContractExecutionResult Execute(ulong blockHeight, uint160 coinbaseAddress);
    }
}
