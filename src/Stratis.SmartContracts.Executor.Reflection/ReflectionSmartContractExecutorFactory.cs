using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public class ReflectionSmartContractExecutorFactory : ISmartContractExecutorFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly ICallDataSerializer serializer;
        private readonly Network network;
        private readonly IStateFactory stateFactory;

        public ReflectionSmartContractExecutorFactory(ILoggerFactory loggerFactory,
            ICallDataSerializer serializer,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            Network network,
            IStateFactory stateFactory)
        {
            this.loggerFactory = loggerFactory;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.serializer = serializer;
            this.network = network;
            this.stateFactory = stateFactory;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public ISmartContractExecutor CreateExecutor(
            IContractStateRoot stateRepository,
            ISmartContractTransactionContext transactionContext)
        {
            return new Executor(this.loggerFactory, this.serializer, 
                    stateRepository, this.refundProcessor, this.transferProcessor, this.network, this.stateFactory);
        }
    }
}