using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public interface IRewindDataIndexStore
    {
        Task FlushAsync(bool force = true);

        Task SaveAsync(uint256 transactionId, int transactionOutputIndex, int rewindDataIndex);
    }
}
