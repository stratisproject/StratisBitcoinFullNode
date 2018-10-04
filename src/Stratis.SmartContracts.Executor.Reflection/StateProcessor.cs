﻿using NBitcoin;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
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

        private StateTransitionResult ApplyCreate(IState state, object[] parameters, byte[] code, BaseMessage message,
            uint160 address,
            string type = null)
        {
            var gasMeter = new GasMeter(message.GasLimit);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            state.ContractState.CreateAccount(address);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, address, message, state.ContractState);

            VmExecutionResult result = this.Vm.Create(state.ContractState, smartContractState, code, parameters, type);

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
            // We need to generate an address here so that we can set the initial balance.
            uint160 address = state.GenerateAddress(this.AddressGenerator);

            // For external creates we need to increment the balance state to take into
            // account any funds sent as part of the original transaction.
            state.AddInitialTransfer(new TransferInfo { Value = message.Amount, To = address, From = message.From});

            return this.ApplyCreate(state, message.Parameters, message.Code, message, address);
        }

        /// <summary>
        /// Applies an internally generated contract creation message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, InternalCreateMessage message)
        {
            bool enoughBalance = this.EnsureSenderHasEnoughBalance(state, message.From, message.Amount);

            if (!enoughBalance)
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            byte[] contractCode = state.ContractState.GetCode(message.From);

            uint160 address = state.GenerateAddress(this.AddressGenerator);

            // For successful internal creates we need to add the transfer to the internal transfer list.
            // For external creates we do not need to do this.
            state.AddInternalTransfer(new TransferInfo
            {
                From = message.From,
                To = address,
                Value = message.Amount
            });

            StateTransitionResult result = this.ApplyCreate(state, message.Parameters, contractCode, message, address, message.Type);
            
            return result;
        }

        private StateTransitionResult ApplyCall(IState state, CallMessage message, byte[] contractCode)
        {
            var gasMeter = new GasMeter(message.GasLimit);

            gasMeter.Spend((Gas)GasPriceList.BaseCost);

            // This needs to happen after the base fee is charged, which is why it's in here.
            // TODO - Remove this check. It isn't possible for the method name to be null.
            if (message.Method.Name == null)
            {
                return StateTransitionResult.Fail(gasMeter.GasConsumed, StateTransitionErrorKind.NoMethodName);
            }

            string type = state.ContractState.GetContractType(message.To);

            ISmartContractState smartContractState = state.CreateSmartContractState(state, gasMeter, message.To, message, state.ContractState);

            VmExecutionResult result = this.Vm.ExecuteMethod(smartContractState, message.Method, contractCode, type);

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
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.InsufficientBalance);

            byte[] contractCode = state.ContractState.GetCode(message.To);

            if (contractCode == null || contractCode.Length == 0)
            {
                return StateTransitionResult.Fail((Gas)0, StateTransitionErrorKind.NoCode);
            }
            
            // For successful internal calls we need to add the transfer to the internal transfer list.
            // For external calls we do not need to do this.
            state.AddInternalTransfer(new TransferInfo
            {
                From = message.From,
                To = message.To,
                Value = message.Amount
            });

            StateTransitionResult result = this.ApplyCall(state, message, contractCode);

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

            // For external calls we need to increment the balance state to take into
            // account any funds sent as part of the original transaction.
            state.AddInitialTransfer(new TransferInfo { Value = message.Amount, To = message.To, From = message.From });

            return this.ApplyCall(state, message, contractCode);
        }

        /// <summary>
        /// Applies an internally generated contract funds transfer message to the current state.
        /// </summary>
        public StateTransitionResult Apply(IState state, ContractTransferMessage message)
        {
            bool enoughBalance = this.EnsureSenderHasEnoughBalance(state, message.From, message.Amount);

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

            // For internal contract-contract transfers we need to add the transfer to the internal transfer list.            
            state.AddInternalTransfer(new TransferInfo
            {
                From = message.From,
                To = message.To,
                Value = message.Amount
            });

            StateTransitionResult result = this.ApplyCall(state, message, contractCode);
            
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