using Microsoft.Extensions.Logging;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public sealed class InternalExecutorFactory : IInternalExecutorFactory
    {
        private readonly IStateProcessor stateProcessor;

        public InternalExecutorFactory(IStateProcessor stateProcessor)
        {
            this.stateProcessor = stateProcessor;
        }

        public IInternalTransactionExecutor Create(IState state, IResourceMeter gasMeter)
        {
            return new InternalExecutor(state, this.stateProcessor, gasMeter);
        }
    }
}