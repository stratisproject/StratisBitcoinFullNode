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
        void Initialize();

        /// <summary>Retrieve the transaction information asynchronously using transaction id.</summary>
        /// <param name="trxid">The transaction id to find.</param>
        Transaction GetTransactionById(uint256 trxid);

        /// <summary>Retrieve transactions information asynchronously using transaction ids.</summary>
        /// <param name="trxids">Ids of transactions to find.</param>
        /// <returns>List of transactions or <c>null</c> if txindexing is disabled.</returns>
        Transaction[] GetTransactionsByIds(uint256[] trxids);

        /// <summary>
        /// Get the corresponding block hash by using transaction hash.
        /// </summary>
        /// <param name="trxid">The transaction hash.</param>
        uint256 GetBlockIdByTransactionId(uint256 trxid);

        /// <summary>
        /// Get the block from the database by using block hash.
        /// </summary>
        /// <param name="blockHash">The block hash.</param>
        Block GetBlock(uint256 blockHash);
    }
}
