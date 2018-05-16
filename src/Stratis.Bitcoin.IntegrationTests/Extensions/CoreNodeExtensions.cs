using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public static class CoreNodeExtensions
    {
        public static Money GetProofOfWorkRewardForMinedBlocks(this CoreNode node, int numberOfBlocks)
        {
            var powValidator = node.FullNode.NodeService<IPowConsensusValidator>();
            var halvingInterval = powValidator.ConsensusParams.SubsidyHalvingInterval;
            var startBlock = node.FullNode.Chain.Height - numberOfBlocks + 1;

            var halvingRange = Enumerable.Range(startBlock, numberOfBlocks).Partition(halvingInterval - 1);
            var totalReward = halvingRange.Sum(p => powValidator.GetProofOfWorkReward(p.First()) * p.Count());

            return totalReward;
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
    }
}
