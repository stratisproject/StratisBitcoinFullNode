using NBitcoin;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IPooledTransaction
    {
        Task<Transaction> GetTransaction(uint256 trxid);
    }
}
