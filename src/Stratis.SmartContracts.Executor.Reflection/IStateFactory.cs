using NBitcoin;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IStateFactory
    {
        IState Create(IContractState stateRoot, IBlock block, ulong txOutValue, uint256 transactionHash);
    }
}