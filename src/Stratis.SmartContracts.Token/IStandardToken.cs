namespace Stratis.SmartContracts.Token
{
    /// <summary>
    /// Interface for a standard smart contract token
    /// </summary>
    public interface IStandardToken
    {
        /// <summary>
        /// Gets the total supply of tokens.
        /// </summary>
        uint TotalSupply { get; }

        /// <summary>
        /// Gets the balance of the specified address.
        /// </summary>
        /// <param name="address">The address to check balance for.</param>
        /// <returns>Balance for the given address</returns>
        uint GetBalance(Address address);

        /// <summary>
        /// Transfers tokens from current address to specified address.
        /// </summary>
        /// <param name="to">Address you want to send tokens to.</param>
        /// <param name="amount">Address to transfer tokens to.</param>
        /// <returns>Result of the transfer operation</returns>
        bool Transfer(Address to, uint amount);

        /// <summary>
        /// Transfers tokens from one address to another.
        /// </summary>
        /// <param name="from">Address to transfer tokens from</param>
        /// <param name="to">Address to transfer tokens to.</param>
        /// <param name="amount">Amount of tokens to transfer.</param>
        /// <returns>Result of the transfer operation</returns>
        bool TransferFrom(Address from, Address to, uint amount);

        /// <summary>
        /// The caller of the approve function approves spender to spend the allowance of tokens.
        /// </summary>
        /// <param name="spender"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        bool Approve(Address spender, uint amount);

        /// <summary>
        /// Returns the amount of tokens owned by the owner that the spender is able to spend.
        /// </summary>
        /// <param name="owner">The address of the owner of the tokens.</param>
        /// <param name="spender">The address of the spender of the tokens.</param>
        /// <returns></returns>
        uint Allowance(Address owner, Address spender);
    }
}