namespace Stratis.SmartContracts.Core.Exceptions
{
    /// <summary>
    /// Thrown when the remaining gas left for contract execution is less than the amount selected to allocate to an internal call. 
    /// <para>
    /// See <see cref="IInternalTransactionExecutor"/>.
    /// </para>
    /// </summary>
    public sealed class InsufficientGasException : SmartContractException
    {
    }
}