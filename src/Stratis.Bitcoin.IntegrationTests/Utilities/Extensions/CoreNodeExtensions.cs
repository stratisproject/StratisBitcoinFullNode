using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Utilities.Extensions
{
    public static class CoreNodeExtensions
    {
        public static Money CalculateProofOfWorkReward(this CoreNode node, int blocksMined)
        {
            var consensusValidator = node.FullNode.NodeService<IPowConsensusValidator>() as PowConsensusValidator;

            var startBlock = node.FullNode.Chain.Height - blocksMined + 1;

            var groupedRewards = Enumerable.Range(startBlock, blocksMined)
                .Partition(consensusValidator.ConsensusParams.SubsidyHalvingInterval);

            var rewardsPerGroup = new List<Money>();

            foreach(var groupedReward in groupedRewards)
            { 
                rewardsPerGroup.Add(groupedReward.Count() * consensusValidator.GetProofOfWorkReward(groupedReward.First()));
            }

            return rewardsPerGroup.Sum();
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
