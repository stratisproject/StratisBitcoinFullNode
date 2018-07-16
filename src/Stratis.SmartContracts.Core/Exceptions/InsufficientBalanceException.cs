namespace Stratis.SmartContracts.Core.Exceptions
{
    /// <summary>
    /// Thrown when the balance of the contract is less than the amount to transfer.
    /// <para>
    /// See <see cref="IInternalTransactionExecutor"/>.
    /// </para>
    /// </summary>
    public sealed class InsufficientBalanceException : SmartContractException
    {
    }
}