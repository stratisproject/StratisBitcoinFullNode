using System;
using NBitcoin;
using Stratis.SmartContracts.Executor.Reflection.Exceptions;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents the kinds of error that can occur during a state transition.
    /// </summary>
    public enum StateTransitionErrorKind
    {
        /// <summary>
        /// The execution ran out of gas.
        /// </summary>
        OutOfGas,

        /// <summary>
        /// The sender did not have enough funds.
        /// </summary>
        InsufficientBalance,

        /// <summary>
        /// The sender did not supply enough gas.
        /// </summary>
        InsufficientGas,

        /// <summary>
        /// The supplied method name was null.
        /// </summary>
        NoMethodName,

        /// <summary>
        /// No contract code was present.
        /// </summary>
        NoCode,

        /// <summary>
        /// An exception was thrown during VM execution.
        /// </summary>
        VmError
    }

    /// <summary>
    /// Represents the result of a successful state transition.
    /// </summary>
    public class StateTransitionSuccess
    {
        public StateTransitionSuccess(
            Gas gasConsumed,
            uint160 contractAddress,
            object result = null)
        {
            this.GasConsumed = gasConsumed;
            this.ContractAddress = contractAddress;
            this.ExecutionResult = result;
        }

        /// <summary>
        /// The result returned by the method being executed on the VM.
        /// </summary>
        public object ExecutionResult { get; }

        /// <summary>
        /// Gas consumed during execution.
        /// </summary>
        public Gas GasConsumed { get; }

        /// <summary>
        /// The receiving contract's address.
        /// </summary>
        public uint160 ContractAddress { get; }

    }

    /// <summary>
    /// Represents the result of a failed state transition.
    /// </summary>
    public class StateTransitionError
    {
        public StateTransitionError(Gas gasConsumed, StateTransitionErrorKind kind, Exception vmException)
        {
            this.Kind = kind;
            this.GasConsumed = gasConsumed;
            this.VmException = vmException;
        }

        /// <summary>
        /// An exception thrown by the VM. This value is null unless <see cref="Kind"/>
        /// equals <see cref="StateTransitionErrorKind.VmError"/> or <see cref="StateTransitionErrorKind.OutOfGas"/>.
        /// </summary>
        public Exception VmException { get; }

        /// <summary>
        /// The kind of error that occurred during the state transition.
        /// </summary>
        public StateTransitionErrorKind Kind { get; }

        /// <summary>
        /// The gas consumed during execution.
        /// </summary>
        public Gas GasConsumed { get; }
    }

    /// <summary>
    /// The result of a state transition operation.
    /// </summary>
    public class StateTransitionResult
    {
        private StateTransitionResult(StateTransitionError error)
        {
            this.IsSuccess = false;
            this.Error = error;
        }

        private StateTransitionResult(StateTransitionSuccess success)
        {
            this.IsSuccess = true;
            this.Success = success;
        }

        /// <summary>
        /// The gas consumed during the state transition.
        /// </summary>
        public Gas GasConsumed => this.IsSuccess ? this.Success.GasConsumed : this.Error.GasConsumed;

        public bool IsSuccess { get; }

        public bool IsFailure => !this.IsSuccess;

        /// <summary>
        /// The successful result of the state transition. This value is null unless
        /// <see cref="IsSuccess"/> equals true.
        /// </summary>
        public StateTransitionSuccess Success { get; }

        /// <summary>
        /// The error result of the state transition. This value is null unless
        /// <see cref="IsFailure"/> equals true.
        /// </summary>
        public StateTransitionError Error { get; }

        /// <summary>
        /// Creates a new result for a successful state transition.
        /// </summary>
        public static StateTransitionResult Ok(Gas gasConsumed,
            uint160 contractAddress,
            object result = null)
        {
            return new StateTransitionResult(
                new StateTransitionSuccess(gasConsumed, contractAddress, result));
        }

        /// <summary>
        /// Creates a new result for a failed state transition due to a VM exception.
        /// </summary>
        public static StateTransitionResult Fail(Gas gasConsumed, Exception vmException)
        {
            StateTransitionErrorKind errorKind = vmException is OutOfGasException
                ? StateTransitionErrorKind.OutOfGas
                : StateTransitionErrorKind.VmError;

            return new StateTransitionResult(new StateTransitionError(gasConsumed, errorKind, vmException));
        }

        /// <summary>
        /// Creates a new result for a failed state transition.
        /// </summary>
        public static StateTransitionResult Fail(Gas gasConsumed, StateTransitionErrorKind kind)
        {
            return new StateTransitionResult(new StateTransitionError(gasConsumed, kind, null));
        }
    }
}