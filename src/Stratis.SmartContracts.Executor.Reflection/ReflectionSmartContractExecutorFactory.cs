using Microsoft.Extensions.Logging;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

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
        private readonly ISmartContractVirtualMachine vm;
        private readonly IContractPrimitiveSerializer contractPrimitiveSerializer;
        private readonly ICallDataSerializer serializer;

        public ReflectionSmartContractExecutorFactory(ILoggerFactory loggerFactory,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            ICallDataSerializer serializer,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            ISmartContractVirtualMachine vm)
        {
            this.loggerFactory = loggerFactory;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.vm = vm;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
            this.serializer = serializer;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public ISmartContractExecutor CreateExecutor(
            IContractState stateRepository,
            ISmartContractTransactionContext transactionContext)
        {
            return new Executor(this.loggerFactory, this.contractPrimitiveSerializer, this.serializer, 
                    stateRepository, this.refundProcessor, this.transferProcessor, this.vm);
        }
    }
}