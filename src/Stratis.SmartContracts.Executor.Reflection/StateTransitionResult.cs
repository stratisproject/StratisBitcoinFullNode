using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// The result of a state transition operation.
    /// </summary>
    public class StateTransitionResult
    {
        public StateTransitionResult(
            Gas gasConsumed, 
            uint160 contractAddress, 
            bool success,
            VmExecutionResult vmExecutionResult = null)
        {
            this.GasConsumed = gasConsumed;
            this.ContractAddress = contractAddress;
            this.Success = success;
            this.VmExecutionResult = vmExecutionResult;
        }

        /// <summary>
        /// The execution result of the VM, or null if the VM was not invoked.
        /// </summary>
        public VmExecutionResult VmExecutionResult { get; }

        /// <summary>
        /// Gas consumed during execution.
        /// </summary>
        public Gas GasConsumed { get; }

        /// <summary>
        /// The receiving contract's address.
        /// </summary>
        public uint160 ContractAddress { get; }

        /// <summary>
        /// Whether the operation was successful.
        /// </summary>
        public bool Success { get; }
    }
}