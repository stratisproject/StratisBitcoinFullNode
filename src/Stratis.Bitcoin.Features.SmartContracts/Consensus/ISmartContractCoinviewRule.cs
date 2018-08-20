using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    public interface ISmartContractCoinviewRule
    {
        ISmartContractExecutorFactory ExecutorFactory { get; }
        ContractStateRepositoryRoot OriginalStateRoot { get; }
    }
}