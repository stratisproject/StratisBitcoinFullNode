namespace Stratis.SmartContracts
{
    /// <summary>
    /// Defines what gets returned should a contract execute a transfer.
    /// </summary>
    public interface ITransferResult
    {
        /// <summary>
        /// The return value of the method called.
        /// </summary>
        /// <remarks>TODO: We should move this to a different result type in the future.</remarks>
        object ReturnValue { get; }

        /// <summary>
        /// Whether execution of the contract method was successful.
        /// </summary>
        bool Success { get; }
    }
}