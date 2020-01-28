using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.ColdStaking.Models;

namespace Stratis.Bitcoin.Features.ColdStaking.Services
{
    public class ColdStakingService : IColdStakingService
    {
        private readonly ColdStakingManager coldStakingManager;

        public ColdStakingService(
            ColdStakingManager coldStakingManager)
        {
            this.coldStakingManager = coldStakingManager;
        }

        public async Task<GetColdStakingInfoResponse> GetColdStakingInfo(string walletName, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                GetColdStakingInfoResponse coldStakingInfo = this.coldStakingManager.GetColdStakingInfo(walletName);

                return new GetColdStakingInfoResponse
                {
                    ColdWalletAccountExists = coldStakingInfo.ColdWalletAccountExists,
                    HotWalletAccountExists = coldStakingInfo.HotWalletAccountExists
                };
            }, cancellationToken);
        }
    }
}
