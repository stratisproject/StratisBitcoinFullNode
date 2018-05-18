using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IBlockStore
    {
        Task<Transaction> GetTrxAsync(uint256 trxid);

        Task<uint256> GetTrxBlockIdAsync(uint256 trxid);

        /// <summary>Fetch the last block stored to disk.</summary>
        ChainedHeader GetHighestPersistedBlock();
    }
}
