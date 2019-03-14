using System.Threading.Tasks;
using Stratis.Bitcoin.NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IPooledTransaction
    {
        Task<Transaction> GetTransaction(uint256 trxid);
    }
}
