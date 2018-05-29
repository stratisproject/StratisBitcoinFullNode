using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.ReflectionExecutor.ContractValidation;

namespace Stratis.SmartContracts.ReflectionExecutor
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public class ReflectionSmartContractExecutorFactory : ISmartContractExecutorFactory
    {
        private readonly ISmartContractCarrierSerializer carrierSerializer;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractReceiptStorage receiptStorage;
        private readonly SmartContractValidator validator;

        public ReflectionSmartContractExecutorFactory(
            ISmartContractCarrierSerializer carrierSerializer,
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            ISmartContractReceiptStorage receiptStorage,
            SmartContractValidator validator)
        {
            this.carrierSerializer = carrierSerializer;
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
        public ISmartContractExecutor CreateExecutor(
            ulong blockHeight,
            uint160 coinbaseAddress,
            Money mempoolFee,
            uint160 sender,
            IContractStateRepository stateRepository,
            Transaction transaction)
        {
            TxOut smartContractTxOut = transaction.Outputs.First(txOut => txOut.ScriptPubKey.IsSmartContractExec);
            if (smartContractTxOut.ScriptPubKey.IsSmartContractCreate)
            {
                return new CreateSmartContract(
                    blockHeight,
                    this.carrierSerializer,
                    coinbaseAddress,
                    this.keyEncodingStrategy,
                    this.loggerFactory,
                    mempoolFee,
                    this.network,
                    this.receiptStorage,
                    sender,
                    stateRepository,
                    transaction,
                    this.validator
                );
            }
            
            return new CallSmartContract(
                blockHeight,
                this.carrierSerializer,
                coinbaseAddress,
                this.keyEncodingStrategy,
                this.loggerFactory,
                mempoolFee,
                this.network,
                this.receiptStorage,
                sender,
                stateRepository,
                transaction,
                this.validator
                );
        }
    }
}