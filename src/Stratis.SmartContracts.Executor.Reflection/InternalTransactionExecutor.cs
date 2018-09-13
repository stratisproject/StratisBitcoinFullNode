using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection
{
    ///<inheritdoc/>
    public sealed class InternalTransactionExecutor : IInternalTransactionExecutor
    {
        private const ulong DefaultGasLimit = GasPriceList.BaseCost * 2 - 1;

        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly IState state;

        public InternalTransactionExecutor(ILoggerFactory loggerFactory, Network network, IState state)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.network = network;
            this.state = state;
        }

        ///<inheritdoc />
        public ICreateResult Create<T>(ISmartContractState smartContractState,
            ulong amountToTransfer,
            object[] parameters,
            ulong gasLimit = 0)
        {
            ulong gasBudget = (gasLimit != 0) ? gasLimit : smartContractState.GasMeter.GasAvailable;

            var message = new InternalCreateMessage(
                smartContractState.Message.ContractAddress.ToUint160(this.network),
                amountToTransfer,
                (Gas) gasBudget,
                parameters,
                typeof(T).Name
            );

            var result = this.state.Apply(message);

            return result.IsSuccess
                ? CreateResult.Succeeded(result.Success.ContractAddress.ToAddress(this.network))
                : CreateResult.Failed();
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
            // For a method call, send all the gas unless an amount was selected.Should only call trusted methods so re - entrance is less problematic.
            ulong gasBudget = (gasLimit != 0) ? gasLimit : smartContractState.GasMeter.GasAvailable;

            var message = new InternalCallMessage(
                addressTo.ToUint160(this.network),
                smartContractState.Message.ContractAddress.ToUint160(this.network),
                amountToTransfer,
                (Gas) gasBudget,
                new MethodCall(methodName, parameters)
            );

            var result = this.state.Apply(message);

            return result.IsSuccess
                ? TransferResult.Transferred(result.Success.ExecutionResult)
                : TransferResult.Failed();
        }

        ///<inheritdoc />
        public ITransferResult Transfer(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(addressTo), addressTo, nameof(amountToTransfer), amountToTransfer);

            ulong gasBudget = DefaultGasLimit; // for Transfer always send limited gas to prevent re-entrance.
           
            var message = new ContractTransferMessage(
                addressTo.ToUint160(this.network),
                smartContractState.Message.ContractAddress.ToUint160(this.network),
                amountToTransfer,
                (Gas) gasBudget
            );
            
            var result = this.state.Apply(message);

            return result.IsSuccess 
                ? TransferResult.Empty() 
                : TransferResult.Failed();
        }
    }
}