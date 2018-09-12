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
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly ISmartContractVirtualMachine vm;
        private readonly IAddressGenerator addressGenerator;
        private readonly IInternalTransactionExecutorFactory internalTransactionExecutorFactory;

        public StateFactory(
            Network network,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            ISmartContractVirtualMachine vm,
            IAddressGenerator addressGenerator,
            IInternalTransactionExecutorFactory internalTransactionExecutorFactory
        )
        {
            this.network = network;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
            this.vm = vm;
            this.addressGenerator = addressGenerator;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
        }

        public IState Create(IContractState stateRoot, IBlock block, ulong txOutValue, uint256 transactionHash)
        {
            return new State(
                this.contractPrimitiveSerializer,
                this.internalTransactionExecutorFactory,
                this.vm,
                stateRoot,
                block,
                this.network,
                txOutValue,
                transactionHash,
                this.addressGenerator);
        }
    }
}