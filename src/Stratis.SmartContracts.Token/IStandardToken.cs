namespace Stratis.SmartContracts.Token
{
    /// <summary>
    /// Interface for a standard smart contract token
    /// </summary>
    public interface IStandardToken
    {
        /// <summary>
        /// The total number of tokens in existence.
        /// </summary>
        uint TotalSupply { get; }

        /// <summary>
        /// Gets the balance of the specified address.
        /// </summary>
        /// <param name="address">The address to query the balance of.</param>
        /// <returns>Balance for the given address</returns>
        uint GetBalance(Address address);

        /// <summary>
        /// Transfers tokens owned by the sender to the destination address.
        /// </summary>
        /// <param name="to">The address to transfer the tokens to.</param>
        /// <param name="amount">The amount of tokens to transfer.</param>
        /// <returns>A bool representing whether the transfer was successful.</returns>
        bool Transfer(Address to, uint amount);

        /// <summary>
        /// Transfers tokens owned by one address to another address.
        /// Message.Sender must have approval to transfer tokens on behalf of the owner.
        /// </summary>
        /// <param name="from">The address to transfer the tokens from.</param>
        /// <param name="to">The address to transfer the tokens to.</param>
        /// <param name="amount">The amount of tokens to transfer.</param>
        /// <returns>A bool representing whether the transfer was successful.</returns>
        bool TransferFrom(Address from, Address to, uint amount);

        /// <summary>
        /// Approves the spender to be able to transfer an amount of tokens on Message.Sender's behalf.
        /// </summary>
        /// <param name="spender">The address to grant approval to.</param>
        /// <param name="amount">To amount of tokens to approve.</param>
        /// <returns>A bool representing whether the approval was successful.</returns>
        bool Approve(Address spender, uint amount);

        /// <summary>
        /// Returns the amount of tokens owned by owner that the spender is able to spend.
        /// </summary>
        /// <param name="owner">The address of the owner of the tokens.</param>
        /// <param name="spender">The address of the spender of the tokens.</param>
        /// <returns>The number of tokens.</returns>
        uint Allowance(Address owner, Address spender);
    }
}