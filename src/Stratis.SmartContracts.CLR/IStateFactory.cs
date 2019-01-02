using NBitcoin;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    public interface IStateFactory
    {
        IState Create(IStateRepository stateRoot, IBlock block, ulong txOutValue, uint256 transactionHash);
    }
}