using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Validation;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Block = Stratis.SmartContracts.Core.Block;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public sealed class CreateSmartContract : ISmartContractExecutor
    {
        private readonly ILogger logger;
        private readonly IContractStateRepository stateSnapshot;
        private readonly SmartContractValidator validator;
        private readonly Network network;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILoggerFactory loggerFactory;
        private readonly ISmartContractResultRefundProcessor refundProcessor;
        private readonly ISmartContractResultTransferProcessor transferProcessor;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ICallDataSerializer serializer;

        public CreateSmartContract(IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network,
            ICallDataSerializer serializer,
            IContractStateRepository stateSnapshot,
            SmartContractValidator validator,
            ISmartContractResultRefundProcessor refundProcessor,
            ISmartContractResultTransferProcessor transferProcessor,
            ISmartContractVirtualMachine vm)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.loggerFactory = loggerFactory;
            this.stateSnapshot = stateSnapshot;
            this.validator = validator;
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.refundProcessor = refundProcessor;
            this.transferProcessor = transferProcessor;
            this.vm = vm;
            this.serializer = serializer;
        }

        public ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext)
        {
            this.logger.LogTrace("()");

            var callDataDeserializationResult = this.serializer.Deserialize(transactionContext.ScriptPubKey.ToBytes());

            // TODO Handle deserialization failure

            var callData = callDataDeserializationResult.Value;

            // Create a new address for the contract.
            uint160 newContractAddress = Core.NewContractAddressExtension.GetContractAddressFromTransactionHash(transactionContext.TransactionHash);

            // Decompile the contract execution code and validate it.
            SmartContractDecompilation decompilation = SmartContractDecompiler.GetModuleDefinition(callData.ContractExecutionCode);
            SmartContractValidationResult validation = this.validator.Validate(decompilation);

            // If validation failed, refund the sender any remaining gas.
            if (!validation.IsValid)
            {
                this.logger.LogTrace("(-)[CONTRACT_VALIDATION_FAILED]");
                return SmartContractExecutionResult.ValidationFailed(validation);
            }

            var gasMeter = new GasMeter(callData.GasLimit);
            
            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            var context = new TransactionContext(
                transactionContext.TransactionHash,
                transactionContext.BlockHeight,
                transactionContext.CoinbaseAddress,
                transactionContext.Sender,
                transactionContext.TxOutValue);
            
            var result = this.vm.Create(gasMeter, this.stateSnapshot, callData, context);

            var revert = result.ExecutionException != null;

            var internalTransaction = this.transferProcessor.Process(
                this.stateSnapshot,
                callData,
                transactionContext,
                result.InternalTransfers, 
                revert);

            (var fee, var refundTxOuts) = this.refundProcessor.Process(
                callData, 
                transactionContext.MempoolFee, 
                transactionContext.Sender,
                result.GasConsumed,
                result.ExecutionException);

            var executionResult = new SmartContractExecutionResult
            {
                NewContractAddress = revert ? null : result.NewContractAddress,
                Exception = result.ExecutionException,
                GasConsumed = result.GasConsumed,
                Return = result.Result,
                InternalTransaction = internalTransaction,
                Fee = fee,
                Refunds = refundTxOuts
            };

            if (revert)
            {
                this.logger.LogTrace("(-)[CONTRACT_EXECUTION_FAILED]");

                this.stateSnapshot.Rollback();
            }
            else
            {
                this.logger.LogTrace("(-):{0}={1}", nameof(newContractAddress), newContractAddress);

                this.stateSnapshot.SetCode(newContractAddress, callData.ContractExecutionCode);

                this.stateSnapshot.Commit();
            }

            return executionResult;
        }
    }
}