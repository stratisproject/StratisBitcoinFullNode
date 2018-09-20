using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Defines how execution context for smart contract executor should be constructed.
    /// </summary>
    public interface IContractExecutionContext
    {
        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        IBlock Block { get; }

        IMessage Message { get; }

        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        ulong GasPrice { get; }

        /// <summary>
        /// These are the method parameters to be injected into the method call by the <see cref="SmartContractExecutor"/>.
        /// </summary>
        object[] Parameters { get; }

        uint160 ContractAddress { get; set; }
    }
}