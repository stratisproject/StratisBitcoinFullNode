using Microsoft.Extensions.Logging;
using NBitcoin;
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
        private readonly Network network;
        private readonly InternalTransactionExecutorFactory internalTransactionExecutorFactory;
        private readonly IAddressGenerator addressGenerator;

        public ReflectionSmartContractExecutorFactory(ILoggerFactory loggerFactory,
            IContractPrimitiveSerializer contractPrimitiveSerializer,
            ICallDataSerializer serializer,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            ISmartContractVirtualMachine vm,
            IAddressGenerator addressGenerator,
            Network network,
            InternalTransactionExecutorFactory internalTransactionExecutorFactory)
        {
            this.loggerFactory = loggerFactory;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.vm = vm;
            this.contractPrimitiveSerializer = contractPrimitiveSerializer;
            this.serializer = serializer;
            this.network = network;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.addressGenerator = addressGenerator;
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
            return new Executor(this.loggerFactory, this.contractPrimitiveSerializer, this.serializer, 
                    stateRepository, this.refundProcessor, this.transferProcessor, this.vm, this.addressGenerator, this.network, this.internalTransactionExecutorFactory);
        }
    }
}