using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.ResultProcessors;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public class ReflectionExecutorFactory : IContractExecutorFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IContractRefundProcessor refundProcessor;
        private readonly IContractTransferProcessor transferProcessor;
        private readonly ICallDataSerializer serializer;
        private readonly Network network;
        private readonly IStateFactory stateFactory;
        private readonly IStateProcessor stateProcessor;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;

        public ReflectionExecutorFactory(ILoggerFactory loggerFactory,
            ICallDataSerializer serializer,
            IContractRefundProcessor refundProcessor,
            IContractTransferProcessor transferProcessor,
            Network network,
            IStateFactory stateFactory,
            IStateProcessor stateProcessor,
            IContractPrimitiveSerializer contractPrimitiveSerializer)
        {
            this.loggerFactory = loggerFactory;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.serializer = serializer;
            this.network = network;
            this.stateFactory = stateFactory;
            this.stateProcessor = stateProcessor;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public IContractExecutor CreateExecutor(
            IStateRepositoryRoot stateRepository,
            IContractTransactionContext transactionContext)
        {
            return new ExternalExecutor(this.loggerFactory, this.serializer, 
                    stateRepository, this.refundProcessor, this.transferProcessor, this.network, this.stateFactory, this.stateProcessor, this.contractPrimitiveSerializer);
        }
    }
}