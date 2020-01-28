using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.ColdStaking.Models;

namespace Stratis.Bitcoin.Features.ColdStaking.Services
{
    public interface IColdStakingService
    {
        Task<GetColdStakingInfoResponse> GetColdStakingInfo(string walletName, CancellationToken cancellationToken);
    }
}
