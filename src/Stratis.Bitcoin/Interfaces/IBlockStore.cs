using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IBlockStore
    {
        Task<Transaction> GetTrxAsync(uint256 trxid);

        Task<uint256> GetTrxBlockIdAsync(uint256 trxid);

        Task<Block> GetBlockAsync(uint256 blockHash);
    }
}
