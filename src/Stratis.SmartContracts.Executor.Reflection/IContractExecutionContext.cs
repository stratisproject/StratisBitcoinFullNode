using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Defines how execution context for smart contract executor should be constructed.
    /// </summary>
    public interface IContractExecutionContext
    {
        /// <summary>
        /// Information about the block the contract being executed is part of.
        /// </summary>
        IBlock Block { get; }

        /// <summary>
        /// Information about the transaction that was sent to trigger this execution.
        /// </summary>
        IMessage Message { get; }

        /// <summary>
        /// These are the method parameters to be injected into the method call by the <see cref="SmartContractExecutor"/>.
        /// </summary>
        object[] Parameters { get; }

        /// <summary>
        /// The 20-byte address of the contract being executed.
        /// </summary>
        uint160 ContractAddress { get; set; }
    }
}