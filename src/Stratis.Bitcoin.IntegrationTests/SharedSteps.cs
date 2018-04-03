using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SharedSteps
    {
        public static TransactionBuildContext CreateTransactionBuildContext(string sendingWalletName, string sendingAccountName, string sendingPassword, Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(new WalletAccountReference(sendingWalletName, sendingAccountName),
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), sendingPassword)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        public void MineBlocks(int blockCount, CoreNode node, string accountName, string toWalletName, string withPassword, long expectedFees = 0)
        {
            this.WaitForBlockStoreToSync(node);

            var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(toWalletName, accountName));

            var balanceBeforeMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Where(x => x.Address == address)
                .Sum(s => s.Transaction.Amount);

            var wallet = node.FullNode.WalletManager().GetWalletByName(toWalletName);
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(withPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);

            var balanceAfterMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Where(x => x.Address == address)
                .Sum(s => s.Transaction.Amount);

            var balanceIncrease = balanceAfterMining - balanceBeforeMining;

            this.WaitForBlockStoreToSync(node);

            var rewardCoinCount = blockCount * Money.COIN * 50;
            
            balanceIncrease.Should().Be(rewardCoinCount + expectedFees);
        }

        public void WaitForBlockStoreToSync(params CoreNode[] nodes)
        {
            if (nodes.Length == 1)
            {
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(nodes[0]));
                return;
            }

            for (int i = 1; i < nodes.Length; i++)
            {
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodes[i-1], nodes[i]));
            }
        }
    }
}
