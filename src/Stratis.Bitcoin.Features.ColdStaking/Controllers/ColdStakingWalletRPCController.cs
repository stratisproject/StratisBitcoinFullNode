using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking.Controllers
{
    /// <summary> All functionality is in WalletRPCController, just inherit the functionality in this feature.</summary>
    public class ColdStakingWalletRPCController : WalletRPCController
    {
          
        public ColdStakingWalletRPCController(IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IFullNode fullNode, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory, WalletSettings walletSettings) : 
            base(walletManager, walletTransactionHandler, fullNode, broadcasterManager, loggerFactory, walletSettings)
        {
        }
    }
}
