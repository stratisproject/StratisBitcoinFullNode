using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Utilities.Extensions;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SharedSteps
    {
        public static TransactionBuildContext CreateTransactionBuildContext(string sendingWalletName
            , string sendingAccountName
            , string sendingPassword
            , ICollection<Recipient> recipients
            , FeeType feeType
            , int minConfirmations)
        {
            return new TransactionBuildContext(new WalletAccountReference(sendingWalletName, sendingAccountName),
                recipients.ToList(), sendingPassword)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        public void MineBlocks(int blockCount, CoreNode node, string accountName, string toWalletName, string withPassword, long expectedFees = 0)
        {
            this.WaitForBlockStoreToSync(node);

            HdAddress address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(toWalletName, accountName));

            Money balanceBeforeMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Where(x => x.Address == address)
                .Sum(s => s.Transaction.Amount);

            Features.Wallet.Wallet wallet = node.FullNode.WalletManager().GetWalletByName(toWalletName);
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(withPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);

            Money balanceAfterMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Where(x => x.Address == address)
                .Sum(s => s.Transaction.Amount);

            Money balanceIncrease = balanceAfterMining - balanceBeforeMining;

            this.WaitForBlockStoreToSync(node);
          
            balanceIncrease.Should().Be(node.CalculateProofOfWorkReward(blockCount) + expectedFees);
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
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodes[i - 1], nodes[i]));
            }
        }
    }
}