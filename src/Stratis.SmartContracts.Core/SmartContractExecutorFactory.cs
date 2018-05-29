using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public sealed class SmartContractExecutorFactory
    {
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractReceiptStorage receiptStorage;
        private readonly SmartContractValidator validator;

        public SmartContractExecutorFactory(
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            ISmartContractReceiptStorage receiptStorage,
            SmartContractValidator validator)
        {
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.network = network;
            this.receiptStorage = receiptStorage;
            this.validator = validator;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public SmartContractExecutor CreateExecutor(SmartContractCarrier carrier, Money mempoolFee, IContractStateRepository stateRepository)
        {
            return SmartContractExecutor.Initialize(carrier, this.network, this.receiptStorage, stateRepository, this.validator, this.keyEncodingStrategy, this.loggerFactory, mempoolFee);
        }
    }
}