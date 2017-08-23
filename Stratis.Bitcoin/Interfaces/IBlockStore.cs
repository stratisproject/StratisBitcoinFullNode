using NBitcoin;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IBlockStore
    {
        Task<Transaction> GetTrxAsync(uint256 trxid);
        Task<uint256> GetTrxBlockIdAsync(uint256 trxid);
    }
}
