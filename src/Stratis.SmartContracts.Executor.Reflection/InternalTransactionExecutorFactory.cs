using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public sealed class InternalTransactionExecutorFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        public InternalTransactionExecutorFactory(ILoggerFactory loggerFactory, Network network)
        {
            this.loggerFactory = loggerFactory;
            this.network = network;
        }

        public IInternalTransactionExecutor Create(IState state)
        {
            return new InternalTransactionExecutor(this.loggerFactory, this.network, state);
        }
    }
}