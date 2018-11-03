using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public interface IRewindDataIndexStore
    {
        Task FlushAsync(bool force = true);

        Task SaveAsync(Dictionary<string, int> indexData);
    }
}
