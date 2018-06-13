using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public static class CoreNodeExtensions
    {
        public static Money GetProofOfWorkRewardForMinedBlocks(this CoreNode node, int numberOfBlocks)
        {
            var coinviewRule = node.FullNode.NodeService<IConsensusRules>().GetRule<CoinViewRule>();

            int startBlock = node.FullNode.Chain.Height - numberOfBlocks + 1;

            return Enumerable.Range(startBlock, numberOfBlocks)
                .Sum(p => coinviewRule.GetProofOfWorkReward(p));
        }


        public static Money WalletBalance(this CoreNode node, string walletName)
        {
            return node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Sum(s => s.Transaction.Amount);
        }

        public static int? WalletHeight(this CoreNode node, string walletName)
        {
            return node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).First().Transaction.BlockHeight;
        }

        public static int WalletSpendableTransactionCount(this CoreNode node, string walletName)
        {
            return node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName).Count();
        }

        public static Money GetFee(this CoreNode node, TransactionBuildContext transactionBuildContext)
        {
            return node.FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);
        }

        public static int GetApiPort(this CoreNode coreNode)
        {
            return coreNode.FullNode.NodeService<ApiSettings>()?.ApiPort ?? -1;
        }
    }
}
