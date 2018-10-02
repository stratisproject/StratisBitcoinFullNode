using System;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    /// <summary>
    /// Represent access to the store of <see cref="Block"/>.
    /// </summary>
    public interface IBlockStore : IDisposable
    {
        /// <summary>
        /// Initializes the blockchain storage and ensure the genesis block has been created in the database.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Retrieve the transaction information asynchronously using transaction id.
        /// </summary>
        /// <param name="trxid">The transaction id to find.</param>
        Task<Transaction> GetTransactionByIdAsync(uint256 trxid);

        /// <summary>
        /// Get the corresponding block hash by using transaction hash.
        /// </summary>
        /// <param name="trxid">The transaction hash.</param>
        Task<uint256> GetBlockIdByTransactionIdAsync(uint256 trxid);

        /// <summary>
        /// Get the block from the database by using block hash.
        /// </summary>
        /// <param name="blockHash">The block hash.</param>
        Task<Block> GetBlockAsync(uint256 blockHash);
    }
}
