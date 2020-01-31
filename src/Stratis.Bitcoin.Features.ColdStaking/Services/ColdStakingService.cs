using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.ColdStaking.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking.Services
{
    public class ColdStakingService : IColdStakingService
    {
        private readonly ColdStakingManager coldStakingManager;

        public ColdStakingService(
            IWalletManager walletManager)
        {
            if (walletManager is ColdStakingManager walletManagerAsColdStakingManager)
            {
                this.coldStakingManager = walletManagerAsColdStakingManager;
            }
            else
            {
                throw new NotSupportedException(
                    "ColdStakingService expects IWalletManager to be of type ColdStakingManager only");
            }
        }

        public async Task<GetColdStakingInfoResponse> GetColdStakingInfo(string walletName,
            CancellationToken cancellationToken)
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