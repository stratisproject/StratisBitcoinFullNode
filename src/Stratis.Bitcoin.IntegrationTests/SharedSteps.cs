using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SharedSteps
    {
        public static TransactionBuildContext CreateTransactionBuildContext(WalletAccountReference accountReference, string password, Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        public void MineBlocks(int blockCount, CoreNode node, string accountName, string toWalletName, string withPassword, int expectedFees = 0)
        {
            this.WaitForBlockStoreToSync(node);

            var balanceBeforeMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Sum(s => s.Transaction.Amount);

            var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(toWalletName, accountName));
            var wallet = node.FullNode.WalletManager().GetWalletByName(toWalletName);
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(withPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);

            var balanceAfterMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Sum(s => s.Transaction.Amount);

            var balanceIncrease = balanceAfterMining - balanceBeforeMining;

            this.WaitForBlockStoreToSync(node);

            var rewardCoinCount = this.GetRewardCoins(node.FullNode.Chain.Height, blockCount, 0);


            
            balanceIncrease.Should().Be(rewardCoinCount + expectedFees);
        }

        private long GetRewardCoins(int currentHeight, int blockCount, long currentTotalCoin)
        {
            int halvingInterval = Network.RegTest.Consensus.SubsidyHalvingInterval;
            long reward = 50 * Money.COIN;

            var maxBlocksAtThisReward = currentHeight;
            while (maxBlocksAtThisReward > halvingInterval)
            {
                reward = reward / 2;
                maxBlocksAtThisReward = maxBlocksAtThisReward - halvingInterval;
            }

            var coinsAtThisRewardLevel = Math.Min(maxBlocksAtThisReward, blockCount) * reward;
            currentTotalCoin += coinsAtThisRewardLevel;

            if (Math.Min(maxBlocksAtThisReward, blockCount) == blockCount)
                return currentTotalCoin;

            return GetRewardCoins(currentHeight - halvingInterval, blockCount - halvingInterval, currentTotalCoin);
        }

        public void WaitForBlockStoreToSync(params CoreNode[] nodes)
        {
            foreach (CoreNode node in nodes)
            {
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node));
            }
        }
    }
}
