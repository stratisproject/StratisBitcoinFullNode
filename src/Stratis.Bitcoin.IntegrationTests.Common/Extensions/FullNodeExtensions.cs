using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public static class FullNodeExtensions
    {
        public static WalletManager WalletManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletManager>() as WalletManager;
        }

        public static WalletTransactionHandler WalletTransactionHandler(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
        }

        public static IConsensusManager ConsensusManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IConsensusManager>() as IConsensusManager;
        }

        public static ICoinView CoinView(this FullNode fullNode)
        {
            return fullNode.NodeService<ICoinView>();
        }

        public static MempoolManager MempoolManager(this FullNode fullNode)
        {
            return fullNode.NodeService<MempoolManager>();
        }

        public static IBlockStore BlockStore(this FullNode fullNode)
        {
            return fullNode.NodeService<IBlockStore>();
        }

        public static ChainedHeader GetBlockStoreTip(this FullNode fullNode)
        {
            return fullNode.NodeService<IChainState>().BlockStoreTip;
        }

        public static HdAddress GetUnusedAddress(this WalletManager walletManager)
        {
            var wallet = walletManager.Wallets.First();
            var walletAccount = wallet.AccountsRoot.First().Accounts.First();
            var walletAccountReference = new WalletAccountReference(wallet.Name, walletAccount.Name);
            return walletManager.GetUnusedAddress(walletAccountReference);
        }
    }
}