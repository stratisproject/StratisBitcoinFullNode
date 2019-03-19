using NBitcoin;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.CLR
{
    public class StateProcessor : IStateProcessor
    {
        public StateProcessor(IVirtualMachine vm,
            IAddressGenerator addressGenerator)
        {
            this.AddressGenerator = addressGenerator;
            this.Vm = vm;
        }

        public IVirtualMachine Vm { get; }

        public IAddressGenerator AddressGenerator { get; }

        private StateTransitionResult ApplyCreate(IState state, 
            object[] parameters, 
            byte[] code, 
            BaseMessage message,
            uint160 address,
            RuntimeObserver.IGasMeter gasMeter,
            string type = null)
        {
            state.ContractState.CreateAccount(address);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, address, message, state.ContractState);

            VmExecutionResult result = this.Vm.Create(state.ContractState, smartContractState, gasMeter, code, parameters, type);

            bool revert = !result.IsSuccess;

            if (revert)
            {
                return StateTransitionResult.Fail(
                    gasMeter.GasConsumed,
                    result.Error);
            }

            return StateTransitionResult.Ok(
                gasMeter.GasConsumed,
                address,
                result.Success.Result
            );
        }

        /// <summary>
        /// Applies an externally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ExternalCreateMessage message)
        {
            var gasMeter = new GasMeter(message.GasLimit);
            gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.CreateCost);

            // We need to generate an address here so that we can set the initial balance.
            uint160 address = state.GenerateAddress(this.AddressGenerator);

            // For external creates we need to increment the balance state to take into
            // account any funds sent as part of the original contract invocation transaction.
            state.AddInitialTransfer(new TransferInfo(message.From, address, message.Amount));

            return this.ApplyCreate(state, message.Parameters, message.Code, message, address, gasMeter);
        }

        /// <summary>
        /// Applies an internally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCreateMessage message)
        {
            bool enoughBalance = this.EnsureSenderHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((RuntimeObserver.Gas)0, StateTransitionErrorKind.InsufficientBalance); // Trivial - just return and let the MethodCall gas account for it.

            var gasMeter = new GasMeter(message.GasLimit);
            gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.CreateCost);

            byte[] contractCode = state.ContractState.GetCode(message.From);

            uint160 address = state.GenerateAddress(this.AddressGenerator);

            // For internal creates we need to add the value contained in the contract invocation transaction
            // to the internal transfer list. This must occur before we apply the message to the state.
            // For external creates we do not need to do this.
            state.AddInternalTransfer(new TransferInfo(message.From, address, message.Amount));

            StateTransitionResult result = this.ApplyCreate(state, message.Parameters, contractCode, message, address, gasMeter, message.Type);
            
            return result;
        }

        private StateTransitionResult ApplyCall(IState state, CallMessage message, byte[] contractCode, RuntimeObserver.IGasMeter gasMeter)
        {
            // This needs to happen after the base fee is charged, which is why it's in here.
            // TODO - Remove this check. It isn't possible for the method name to be null.
            if (message.Method.Name == null)
            {
                return StateTransitionResult.Fail(gasMeter.GasConsumed, StateTransitionErrorKind.NoMethodName);
            }

            string type = state.ContractState.GetContractType(message.To);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, message.To, message, state.ContractState);

            VmExecutionResult result = this.Vm.ExecuteMethod(smartContractState, gasMeter, message.Method, contractCode, type);

            bool revert = !result.IsSuccess;

            if (revert)
            {
                return StateTransitionResult.Fail(
                    gasMeter.GasConsumed,
                    result.Error);
            }

            return StateTransitionResult.Ok(
                gasMeter.GasConsumed,
                message.To,
                result.Success.Result
            );
        }

        /// <summary>
        /// Applies an internally generated contract method call message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCallMessage message)
        {
            bool enoughBalance = this.EnsureSenderHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((RuntimeObserver.Gas)0, StateTransitionErrorKind.InsufficientBalance); // Trivial - just return and let the MethodCall gas account for it.

            var gasMeter = new GasMeter(message.GasLimit);
            gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.BaseCost);

            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail(gasMeter.GasConsumed, StateTransitionErrorKind.NoCode);
            }

            // For internal calls we need to add the value contained in the contract invocation transaction
            // to the internal transfer list. This must occur before we apply the message to the state.
            // For external calls we do not need to do this.
            state.AddInternalTransfer(new TransferInfo(message.From, message.To, message.Amount));

            StateTransitionResult result = this.ApplyCall(state, message, contractCode, gasMeter);

            return result;
        }

        /// <summary>
        /// Applies an externally generated contract method call message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ExternalCallMessage message)
        {
            var gasMeter = new GasMeter(message.GasLimit);
            gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.BaseCost);

            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail(gasMeter.GasConsumed, StateTransitionErrorKind.NoCode);
            }

            // For external calls we need to increment the balance state to take into
            // account any funds sent as part of the original contract invocation transaction.
            state.AddInitialTransfer(new TransferInfo(message.From, message.To, message.Amount));

            return this.ApplyCall(state, message, contractCode, gasMeter);
        }

        /// <summary>
        /// Applies an internally generated contract funds transfer message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ContractTransferMessage message)
        {
            bool enoughBalance = this.EnsureSenderHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((RuntimeObserver.Gas)0, StateTransitionErrorKind.InsufficientBalance);

            var gasMeter = new GasMeter(message.GasLimit);

            // If it's not a contract, create a regular P2PKH tx
            // If it is a contract, do a regular contract call
            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.TransferCost);

                // No contract at this address, create a regular P2PKH xfer
                state.AddInternalTransfer(new TransferInfo(message.From, message.To, message.Amount));

                return StateTransitionResult.Ok(gasMeter.GasConsumed, message.To);
            }

            // For internal contract-contract transfers we need to add the value contained in the contract invocation transaction
            // to the internal transfer list. This must occur before we apply the message to the state.
            state.AddInternalTransfer(new TransferInfo(message.From, message.To, message.Amount));

            gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.BaseCost);
            StateTransitionResult result = this.ApplyCall(state, message, contractCode, gasMeter);
            
            return result;
        }

        /// <summary>
        /// Checks whether a contract has enough funds to make this transaction.
        /// </summary>
        private bool EnsureSenderHasEnoughBalance(IState state, uint160 contractAddress, ulong amountToTransfer)
        {
            ulong balance = state.GetBalance(contractAddress);

            return balance >= amountToTransfer;
        }
    }
}