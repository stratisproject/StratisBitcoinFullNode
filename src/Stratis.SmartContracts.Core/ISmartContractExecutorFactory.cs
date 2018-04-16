using NBitcoin;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractExecutorFactory
    {
        ISmartContractExecutor CreateExecutor(
            SmartContractCarrier carrier,
            Money mempoolFee,
            IContractStateRepository stateRepository);
    }
}
