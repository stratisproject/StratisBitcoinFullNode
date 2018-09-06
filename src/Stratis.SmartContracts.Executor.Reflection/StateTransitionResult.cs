using System;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public enum StateTransitionErrorKind
    {
        OutOfGas,
        InsufficientBalance,
        InsufficientGas,
        NoMethodName,
        NoCode,
        VmError
    }

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

    public class StateTransitionError
    {
        public StateTransitionError(Gas gasConsumed, StateTransitionErrorKind kind, Exception vmException)
        {
            this.Kind = kind;
            this.GasConsumed = gasConsumed;
            this.VmException = vmException;
        }

        public Exception VmException { get; }

        public StateTransitionErrorKind Kind { get; }

        public Gas GasConsumed { get; }
    }

    /// <summary>
    /// The result of a state transition operation. A successful or failed operation can still have gas consumed.
    ///
    /// A successful operation will have the contract's address.
    ///
    /// A failed operation will have the error. Even if an error is produced, the operation is still included in a block.
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

        public Gas GasConsumed => this.IsSuccess ? this.Success.GasConsumed : this.Error.GasConsumed;

        public bool IsSuccess { get; }

        public bool IsFailure => !this.IsSuccess;

        public StateTransitionSuccess Success { get; }

        public StateTransitionError Error { get; }

        public static StateTransitionResult Ok(Gas gasConsumed,
            uint160 contractAddress,
            object result = null)
        {
            return new StateTransitionResult(
                new StateTransitionSuccess(gasConsumed, contractAddress, result));
        }

        public static StateTransitionResult Fail(Gas gasConsumed, StateTransitionErrorKind kind, Exception vmException)
        {
            return new StateTransitionResult(new StateTransitionError(gasConsumed, kind, vmException));
        }

        public static StateTransitionResult Fail(Gas gasConsumed, StateTransitionErrorKind kind)
        {
            return new StateTransitionResult(new StateTransitionError(gasConsumed, kind, null));
        }
    }
}