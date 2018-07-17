using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public class ReflectionSmartContractExecutorFactory : ISmartContractExecutorFactory
    {
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly SmartContractValidator validator;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ICallDataSerializer serializer;

        public ReflectionSmartContractExecutorFactory(IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            ICallDataSerializer serializer,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            SmartContractValidator validator,
            ISmartContractVirtualMachine vm)
        {
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.network = network;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.validator = validator;
            this.vm = vm;
            this.serializer = serializer;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public ISmartContractExecutor CreateExecutor(
            IContractStateRepository stateRepository,
            ISmartContractTransactionContext transactionContext)
        {
            if (transactionContext.IsCreate)
            {
                return new CreateSmartContract(this.keyEncodingStrategy, this.loggerFactory, this.network, this.serializer,
                    stateRepository, this.validator, this.refundProcessor, this.transferProcessor, this.vm);
            }

            return new CallSmartContract(this.keyEncodingStrategy, this.loggerFactory, this.network, this.serializer, 
                    stateRepository, this.refundProcessor, this.transferProcessor, this.vm);
        }
    }
}