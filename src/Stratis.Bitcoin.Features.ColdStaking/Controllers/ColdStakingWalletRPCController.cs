using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking.Controllers
{
    /// <summary> All functionality is in WalletRPCController, just inherit the functionality in this feature.</summary>
    public class ColdStakingWalletRPCController : WalletRPCController
    {
        public ColdStakingWalletRPCController(
            IBroadcasterManager broadcasterManager,
            ChainIndexer chainIndexer,
            IConsensusManager consensusManager,
            IFullNode fullNode,
            ILoggerFactory loggerFactory,
            Network network,
            IScriptAddressReader scriptAddressReader,
            NodeSettings nodeSettings,
            IWalletManager walletManager,
            WalletSettings walletSettings,
            IWalletTransactionHandler walletTransactionHandler) :
            base(broadcasterManager, chainIndexer, consensusManager, fullNode, loggerFactory, network, scriptAddressReader, nodeSettings, walletManager, walletSettings, walletTransactionHandler)
        {
        }
    }
}