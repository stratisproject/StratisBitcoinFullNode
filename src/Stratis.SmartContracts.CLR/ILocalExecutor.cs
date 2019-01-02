using NBitcoin;
using Stratis.SmartContracts.CLR.Local;

namespace Stratis.SmartContracts.CLR
{
    public interface ILocalExecutor
    {
        ILocalExecutionResult Execute(ulong blockHeight, uint160 sender, Money txOutValue, ContractTxData txData);
    }
}
