namespace Stratis.SmartContracts.Standards
{
    /// <summary>
    /// Interface for a token that can be minted
    /// </summary>
    public interface IMintableToken
    {
        /// <summary>
        /// Mints a specified amount of tokens to a specified address.
        /// </summary>
        /// <param name="to">Address you want to mint tokens in.</param>
        /// <param name="amountToMint">Amount of tokens to mint.</param>
        /// <returns>Result of the mint operation</returns>
        bool Mint(Address to, ulong amountToMint);
    }
}
