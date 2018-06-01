using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public sealed class InternalTransactionExecutorFactory
    {
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        public InternalTransactionExecutorFactory(IKeyEncodingStrategy keyEncodingStrategy, ILoggerFactory loggerFactory, Network network)
        {
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.network = network;
        }

        public IInternalTransactionExecutor Create(IContractStateRepository stateRepository, InternalTransferList internalTransferList)
        {
            return new InternalTransactionExecutor(stateRepository, internalTransferList, this.keyEncodingStrategy, this.loggerFactory, this.network);
        }
    }
}