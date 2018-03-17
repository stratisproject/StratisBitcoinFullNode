namespace Stratis.SmartContracts.Core.Backend
{
    /// <summary>
    /// Defines how execution context for smart contract executor should be constructed.
    /// </summary>
    public interface ISmartContractExecutionContext
    {
        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        Block Block { get; }

        Message Message { get; }

        /// <summary>
        /// TODO: Add documentation.
        /// </summary>
        ulong GasPrice { get; }

        /// <summary>
        /// These are the method parameters to be injected into the method call by the <see cref="SmartContractTransactionExecutor"/>.
        /// </summary>
        object[] Parameters { get; }
    }
}