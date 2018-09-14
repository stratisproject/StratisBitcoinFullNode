using NBitcoin;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class StateProcessor : IStateProcessor
    {
        public StateProcessor(ISmartContractVirtualMachine vm,
            IAddressGenerator addressGenerator)
        {
            this.AddressGenerator = addressGenerator;
            this.Vm = vm;
        }

        public ISmartContractVirtualMachine Vm { get; }

        public IAddressGenerator AddressGenerator { get; }

        private StateTransitionResult ApplyCreate(IState state, object[] parameters, byte[] code, BaseMessage message, string type = null)
        {
            var gasMeter = new GasMeter(message.GasLimit);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            uint160 address = state.GenerateAddress(this.AddressGenerator);

            state.ContractState.CreateAccount(address);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, address, message, state.ContractState);

            VmExecutionResult result = this.Vm.Create(state.ContractState, smartContractState, code, parameters, type);

            bool revert = result.ErrorMessage != null;

            if (revert)
            {
                return StateTransitionResult.Fail(
                    gasMeter.GasConsumed,
                    result.ErrorMessage);
            }

            return StateTransitionResult.Ok(
                gasMeter.GasConsumed,
                address,
                result.Result
            );
        }

        /// <summary>
        /// Applies an externally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ExternalCreateMessage message)
        {
            return this.ApplyCreate(state, message.Parameters, message.Code, message);
        }

        /// <summary>
        /// Applies an internally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCreateMessage message)
        {
            bool enoughBalance = this.EnsureContractHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            byte[] contractCode = state.ContractState.GetCode(message.From);

            StateTransitionResult result = this.ApplyCreate(state, message.Parameters, contractCode, message, message.Type);

            // For successful internal creates we need to add the transfer to the internal transfer list.
            // For external creates we do not need to do this.
            if (result.IsSuccess)
            {
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = result.Success.ContractAddress,
                    Value = message.Amount
                });
            }

            return result;
        }

        private StateTransitionResult ApplyCall(IState state, CallMessage message, byte[] contractCode)
        {
            var gasMeter = new GasMeter(message.GasLimit);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            // This needs to happen after the base fee is charged, which is why it's in here.
            if (message.Method.Name == null)
            {
                return StateTransitionResult.Fail(gasMeter.GasConsumed, StateTransitionErrorKind.NoMethodName);
            }

            string type = state.ContractState.GetContractType(message.To);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, message.To, message, state.ContractState);

            VmExecutionResult result = this.Vm.ExecuteMethod(smartContractState, message.Method, contractCode, type);

            bool revert = result.ErrorMessage != null;

            if (revert)
            {
                return StateTransitionResult.Fail(
                    gasMeter.GasConsumed,
                    result.ErrorMessage);
            }

            return StateTransitionResult.Ok(
                gasMeter.GasConsumed,
                message.To,
                result.Result
            );
        }

        /// <summary>
        /// Applies an internally generated contract method call message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCallMessage message)
        {
            bool enoughBalance = this.EnsureContractHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.NoCode);
            }

            StateTransitionResult result = this.ApplyCall(state, message, contractCode);

            // For successful internal calls we need to add the transfer to the internal transfer list.
            // For external calls we do not need to do this.
            if (result.IsSuccess)
            {
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = message.To,
                    Value = message.Amount
                });
            }

            return result;
        }

        /// <summary>
        /// Applies an externally generated contract method call message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ExternalCallMessage message)
        {
            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.NoCode);
            }

            return this.ApplyCall(state, message, contractCode);
        }

        /// <summary>
        /// Applies an internally generated contract funds transfer message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ContractTransferMessage message)
        {
            bool enoughBalance = this.EnsureContractHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            // If it's not a contract, create a regular P2PKH tx
            // If it is a contract, do a regular contract call
            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                // No contract at this address, create a regular P2PKH xfer
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = message.To,
                    Value = message.Amount
                });

                return StateTransitionResult.Ok((Gas)0, message.To);
            }

            StateTransitionResult result = this.ApplyCall(state, message, contractCode);

            // For successful internal contract-contract transfers we need to add the transfer to the internal transfer list.
            if (result.IsSuccess)
            {
                state.AddInternalTransfer(new TransferInfo
                {
                    From = message.From,
                    To = message.To,
                    Value = message.Amount
                });
            }

            return result;
        }

        /// <summary>
        /// Checks whether a contract has enough funds to make this transaction.
        /// </summary>
        private bool EnsureContractHasEnoughBalance(IState state, uint160 contractAddress, ulong amountToTransfer)
        {
            ulong balance = state.GetBalance(contractAddress);

            return balance >= amountToTransfer;
        }
    }
}