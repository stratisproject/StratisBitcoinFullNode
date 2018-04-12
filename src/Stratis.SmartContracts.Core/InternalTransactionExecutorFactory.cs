using NBitcoin;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public class InternalTransactionExecutorFactory
    {
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;

        public InternalTransactionExecutorFactory(Network network, IKeyEncodingStrategy keyEncodingStrategy)
        {
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
        }

        public IInternalTransactionExecutor Create(IContractStateRepository stateRepository, InternalTransferList internalTransferList)
        {
            return new InternalTransactionExecutor(stateRepository, this.network, this.keyEncodingStrategy, internalTransferList);
        }
    }
}