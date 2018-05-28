using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractExecutor
    {
        ISmartContractExecutionResult Execute(ulong blockHeight, uint160 coinbaseAddress);
    }
}
