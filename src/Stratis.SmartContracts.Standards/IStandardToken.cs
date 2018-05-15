namespace Stratis.SmartContracts.Standards
{
    /// <summary>
    /// Interface for a standard smart contract token
    /// </summary>
    public interface IStandardToken
    {
        /// <summary>
        /// Gets the total supply of tokens.
        /// </summary>
        ulong TotalSupply { get; }

        /// <summary>
        /// Gets the balance of the specified address.
        /// </summary>
        /// <param name="address">The address to check balance for.</param>
        /// <returns>Balance for the given address</returns>
        ulong GetBalance(Address address);

        /// <summary>
        /// Transfers tokens from current address to specified address.
        /// </summary>
        /// <param name="to">Address you want to send tokens to.</param>
        /// <param name="amountToTransfer">Address to transfer tokens to.</param>
        /// <returns>Result of the transfer operation</returns>
        bool Transfer(Address to, ulong amountToTransfer);

        /// <summary>
        /// Transfers tokens from one address to another.
        /// </summary>
        /// <param name="from">Address to transfer tokens from</param>
        /// <param name="to">Address to transfer tokens to.</param>
        /// <param name="amountToTransfer">Amount of tokens to transfer.</param>
        /// <returns>Result of the transfer operation</returns>
        bool TransferFrom(Address from, Address to, ulong amountToTransfer);
    }
}
