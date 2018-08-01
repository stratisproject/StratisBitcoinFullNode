using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    /// <summary>
    /// Represent access to the store of <see cref="Block"/>.
    /// </summary>
    public interface IBlockStore
    {
        /// <summary>
        /// Get a transaction, this is valid when <see cref="StoreSettings.ReIndex"/> is enabled.
        /// </summary>
        /// <param name="trxid">The transaction hash.</param>
        Task<Transaction> GetTrxAsync(uint256 trxid);

        /// <summary>
        /// Get the block associated with a transaction, this is valid when <see cref="StoreSettings.ReIndex"/> is enabled.
        /// </summary>
        /// <param name="trxid"></param>
        Task<uint256> GetTrxBlockIdAsync(uint256 trxid);

        /// <summary>
        /// Get an instance of a block.
        /// </summary>
        /// <param name="blockHash">The block hash.</param>
        Task<Block> GetBlockAsync(uint256 blockHash);

        /// <summary>
        /// Initialize the block store.
        /// </summary>
        Task InitializeAsync();
    }
}
