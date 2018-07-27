using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

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

        public InternalTransactionExecutorFactory(IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory, Network network)
        {
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.network = network;
        }

        public IInternalTransactionExecutor Create(ISmartContractVirtualMachine vm,
            IContractStateRepository stateRepository, List<TransferInfo> internalTransferList,
            ITransactionContext transactionContext)
        {
            return new InternalTransactionExecutor(transactionContext, vm, stateRepository, internalTransferList, this.keyEncodingStrategy, this.loggerFactory, this.network);
        }
    }
}