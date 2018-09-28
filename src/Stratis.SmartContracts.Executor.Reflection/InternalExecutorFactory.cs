using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public sealed class InternalExecutorFactory : IInternalExecutorFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly IStateProcessor stateProcessor;

        public InternalExecutorFactory(ILoggerFactory loggerFactory, Network network, IStateProcessor stateProcessor)
        {
            this.loggerFactory = loggerFactory;
            this.network = network;
            this.stateProcessor = stateProcessor;
        }

        public IInternalTransactionExecutor Create(IState state)
        {
            return new InternalExecutor(this.loggerFactory, this.network, state, this.stateProcessor);
        }
    }
}