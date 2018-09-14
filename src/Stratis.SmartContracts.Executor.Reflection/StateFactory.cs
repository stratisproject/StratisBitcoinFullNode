using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Creates a new <see cref="State"/> object.
    /// </summary>
    public class StateFactory : IStateFactory
    {
        private readonly Network network;
        private readonly IInternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly ISmartContractStateFactory smartContractStateFactory;

        public StateFactory(Network network,
            ISmartContractStateFactory smartContractStateFactory)
        {
            this.network = network;
            this.smartContractStateFactory = smartContractStateFactory;
        }

        public IState Create(IContractState stateRoot, IBlock block, ulong txOutValue, uint256 transactionHash)
        {
            return new State(stateRoot,
                block,
                this.network,
                txOutValue,
                transactionHash,
                this.smartContractStateFactory);
        }
    }
}