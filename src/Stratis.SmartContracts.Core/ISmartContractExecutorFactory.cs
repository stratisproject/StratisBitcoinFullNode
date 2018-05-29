using NBitcoin;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractExecutorFactory
    {
        ISmartContractExecutor CreateExecutor(
            ulong blockHeight,
            uint160 coinbaseAddress,
            Money mempoolFee,
            uint160 sender,
            IContractStateRepository stateRepository,
            Transaction transaction);
    }
}
