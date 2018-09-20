using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class SmartContractStateFactory : ISmartContractStateFactory
    {
        public SmartContractStateFactory(IContractPrimitiveSerializer serializer,
            Network network,
            IInternalExecutorFactory internalTransactionExecutorFactory)
        {
            this.Serializer = serializer;
            this.Network = network;
            this.InternalTransactionExecutorFactory = internalTransactionExecutorFactory;
        }

        public Network Network { get; }
        public IContractPrimitiveSerializer Serializer { get; }
        public IInternalExecutorFactory InternalTransactionExecutorFactory { get; }

        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>        
        public ISmartContractState Create(IState state, IGasMeter gasMeter, uint160 address, BaseMessage message, IStateRepository repository)
        {
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy());

            var persistentState = new PersistentState(persistenceStrategy, this.Serializer, address);

            var contractLogger = new MeteredContractLogger(gasMeter, state.LogHolder, this.Network, this.Serializer);

            var contractState = new SmartContractState(
                state.Block,
                new Message(
                    address.ToAddress(this.Network),
                    message.From.ToAddress(this.Network),
                    message.Amount
                ),
                persistentState,
                this.Serializer,
                gasMeter,
                contractLogger,
                this.InternalTransactionExecutorFactory.Create(state),
                new InternalHashHelper(),
                () => state.GetBalance(address));

            return contractState;
        }
    }
}