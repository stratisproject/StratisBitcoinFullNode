using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Exceptions;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;

namespace Stratis.SmartContracts.Executor.Reflection
{
    ///<inheritdoc/>
    public sealed class InternalTransactionExecutor : IInternalTransactionExecutor
    {
        private const ulong DefaultGasLimit = GasPriceList.BaseCost - 1;

        private readonly IAddressGenerator addressGenerator;
        private readonly IContractLogHolder contractLogHolder;
        private readonly IContractState contractStateRepository;
        private readonly List<TransferInfo> internalTransferList;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly ISmartContractVirtualMachine vm;
        private readonly ITransactionContext transactionContext;

        public InternalTransactionExecutor(ITransactionContext transactionContext,
            ISmartContractVirtualMachine vm,
            IContractLogHolder contractLogHolder,
            IContractState contractStateRepository,
            List<TransferInfo> internalTransferList,
            IKeyEncodingStrategy keyEncodingStrategy,
            ILoggerFactory loggerFactory,
            Network network)
        {
            this.transactionContext = transactionContext;
            this.contractLogHolder = contractLogHolder;
            this.contractStateRepository = contractStateRepository;
            this.internalTransferList = internalTransferList;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.network = network;
            this.vm = vm;
        }

        ///<inheritdoc />
        public ICreateResult Create<T>(ISmartContractState smartContractState,
            ulong amountToTransfer,
            object[] parameters,
            ulong gasLimit = 0)
        {
            // TODO: Expend any neccessary costs.

            ulong gasBudget = (gasLimit != 0) ? gasLimit : DefaultGasLimit;

            // Ensure we have enough gas left to be able to fund the new GasMeter.
            if (smartContractState.GasMeter.GasAvailable < gasBudget)
                throw new InsufficientGasException();

            var nestedGasMeter = new GasMeter((Gas)gasBudget);

            // Check balance.
            EnsureContractHasEnoughBalance(smartContractState, amountToTransfer);

            // Build objects for VM
            byte[] contractCode = this.contractStateRepository.GetCode(smartContractState.Message.ContractAddress.ToUint160(this.network)); // TODO: Fix this when calling from constructor.

            var context = new TransactionContext(
                this.transactionContext.TransactionHash,
                this.transactionContext.BlockHeight,
                this.transactionContext.Coinbase,
                smartContractState.Message.ContractAddress.ToUint160(this.network),
                amountToTransfer,
                this.transactionContext.GetNonceAndIncrement());

            IContractState track = this.contractStateRepository.StartTracking();

            var createData = new CreateData(nestedGasMeter.GasLimit, contractCode, parameters);

            // Do create in vm
            VmExecutionResult result = this.vm.Create(nestedGasMeter, track, createData, context, typeof(T).Name);

            // Update parent gas meter.
            smartContractState.GasMeter.Spend(nestedGasMeter.GasConsumed);

            var revert = result.ExecutionException != null;

            if (revert)
            {
                this.logger.LogTrace("(-)[CONTRACT_EXECUTION_FAILED]");
                track.Rollback();
                return CreateResult.Failed();
            }

            this.logger.LogTrace("(-)[CONTRACT_EXECUTION_SUCCEEDED]");
            track.Commit();

            this.internalTransferList.Add(new TransferInfo
            {
                From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                To = result.NewContractAddress,
                Value = amountToTransfer
            });

            this.contractLogHolder.AddRawLogs(result.RawLogs);

            return CreateResult.Succeeded(result.NewContractAddress.ToAddress(this.network));
        }

        ///<inheritdoc />
        public ITransferResult Call(
            ISmartContractState smartContractState,
            Address addressTo,
            ulong amountToTransfer,
            string methodName,
            object[] parameters,
            ulong gasLimit = 0)
        {
            // TODO: Spend BaseFee here

            EnsureContractHasEnoughBalance(smartContractState, amountToTransfer);

            byte[] contractCode = this.contractStateRepository.GetCode(addressTo.ToUint160(this.network));
            if (contractCode == null || contractCode.Length == 0)
            {
                return TransferResult.Empty();
            }

            // Here, we know contract has code, so we execute it
            // For a method call, send all the gas unless an amount was selected.Should only call trusted methods so re - entrance is less problematic.
            ulong gasBudget = (gasLimit != 0) ? gasLimit : smartContractState.GasMeter.GasAvailable;

            return ExecuteTransferFundsToContract(contractCode, smartContractState, addressTo, amountToTransfer, methodName, parameters, gasBudget);
        }

        ///<inheritdoc />
        public ITransferResult Transfer(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(addressTo), addressTo, nameof(amountToTransfer), amountToTransfer);

            // TODO: Spend BaseFee here

            EnsureContractHasEnoughBalance(smartContractState, amountToTransfer);

            // Discern whether this is a contract or an ordinary address.
            byte[] contractCode = this.contractStateRepository.GetCode(addressTo.ToUint160(this.network));
            if (contractCode == null || contractCode.Length == 0)
            {
                this.internalTransferList.Add(new TransferInfo
                {
                    From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                    To = addressTo.ToUint160(this.network),
                    Value = amountToTransfer
                });

                this.logger.LogTrace("(-)[TRANSFER_TO_SENDER]:Transfer {0} from {1} to {2}.", smartContractState.Message.ContractAddress, addressTo, amountToTransfer);
                return TransferResult.Empty();
            }

            this.logger.LogTrace("(-)[TRANSFER_TO_CONTRACT]");

            // Calling a receive handler:
            string methodName = MethodCall.ExternalReceiveHandlerName;
            object[] parameters = new object[] { };
            ulong gasBudget = DefaultGasLimit; // for Transfer always send limited gas to prevent re-entrance.

            return ExecuteTransferFundsToContract(contractCode, smartContractState, addressTo, amountToTransfer, methodName, parameters, gasBudget);
        }

        /// <summary>
        /// If the address to where the funds will be tranferred to is a contract, instantiate and execute it.
        /// </summary>
        private ITransferResult ExecuteTransferFundsToContract(
            byte[] contractCode,
            ISmartContractState smartContractState,
            Address addressTo,
            ulong amountToTransfer,
            string methodName,
            object[] parameters,
            ulong gasBudget)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(addressTo), addressTo, nameof(amountToTransfer), amountToTransfer);

            // Ensure we have enough gas left to be able to fund the new GasMeter.
            if (smartContractState.GasMeter.GasAvailable < gasBudget)
                throw new InsufficientGasException();

            var nestedGasMeter = new GasMeter((Gas)gasBudget);

            IContractState track = this.contractStateRepository.StartTracking();

            var callData = new CallData((Gas) gasBudget, addressTo.ToUint160(this.network), methodName, parameters);
            
            var context = new TransactionContext(
                this.transactionContext.TransactionHash,
                this.transactionContext.BlockHeight,
                this.transactionContext.Coinbase,
                smartContractState.Message.ContractAddress.ToUint160(this.network),
                amountToTransfer,
                this.transactionContext.Nonce);

            VmExecutionResult result = this.vm.ExecuteMethod(
                nestedGasMeter, 
                track, 
                callData,
                context);

            // Update parent gas meter.
            smartContractState.GasMeter.Spend(nestedGasMeter.GasConsumed);

            var revert = result.ExecutionException != null;

            if (revert)
            {
                track.Rollback();
                return TransferResult.Failed(result.ExecutionException);
            }

            track.Commit();

            this.internalTransferList.Add(new TransferInfo
            {
                From = smartContractState.Message.ContractAddress.ToUint160(this.network),
                To = addressTo.ToUint160(this.network),
                Value = amountToTransfer
            });

            this.contractLogHolder.AddRawLogs(result.RawLogs);

            this.logger.LogTrace("(-)");

            return TransferResult.Transferred(result.Result);
        }

        /// <summary>
        /// Throws an exception if a contract doesn't have a high enough balance to make this transaction.
        /// </summary>
        private void EnsureContractHasEnoughBalance(ISmartContractState smartContractState, ulong amountToTransfer)
        {
            ulong balance = smartContractState.GetBalance();
            if (balance < amountToTransfer)
            {
                this.logger.LogTrace("(-)[INSUFFICIENT_BALANCE]:{0}={1}", nameof(balance), balance);
                throw new InsufficientBalanceException();
            }
        }
    }
}