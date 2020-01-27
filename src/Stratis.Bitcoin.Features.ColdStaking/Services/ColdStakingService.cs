using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.ColdStaking.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;

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
