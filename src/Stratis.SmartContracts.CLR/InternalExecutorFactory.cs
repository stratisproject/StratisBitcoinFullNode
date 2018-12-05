using Microsoft.Extensions.Logging;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public sealed class InternalExecutorFactory : IInternalExecutorFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IStateProcessor stateProcessor;

        public InternalExecutorFactory(ILoggerFactory loggerFactory, IStateProcessor stateProcessor)
        {
            this.loggerFactory = loggerFactory;
            this.stateProcessor = stateProcessor;
        }

        public IInternalTransactionExecutor Create(IState state)
        {
            return new InternalExecutor(this.loggerFactory, state, this.stateProcessor);
        }
    }
}