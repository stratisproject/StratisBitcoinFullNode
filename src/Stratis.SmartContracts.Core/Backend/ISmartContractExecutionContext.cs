namespace Stratis.SmartContracts.Core.Backend
{
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
        /// These are the method parameters to be injected into the method call by the <see cref="Stratis.SmartContracts.Core.SmartContractTransactionExecutor"/>.
        /// </summary>
        object[] Parameters { get; }
    }
}