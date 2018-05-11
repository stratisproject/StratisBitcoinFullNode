using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.State;

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
        private readonly SmartContractValidator validator;

        public SmartContractExecutorFactory(
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            SmartContractValidator validator)
        {
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.network = network;
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
            return SmartContractExecutor.Initialize(carrier, this.network, stateRepository, this.validator, this.keyEncodingStrategy, this.loggerFactory, mempoolFee);
        }
    }
}