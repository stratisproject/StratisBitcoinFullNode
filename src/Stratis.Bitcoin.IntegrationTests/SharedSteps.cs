using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SharedSteps
    {
        public static TransactionBuildContext CreateTransactionBuildContext(
            string sendingWalletName,
            string sendingAccountName,
            string sendingPassword,
            ICollection<Recipient> recipients,
            FeeType feeType,
            int minConfirmations)
        {
            return new TransactionBuildContext(new WalletAccountReference(sendingWalletName, sendingAccountName), recipients.ToList(), sendingPassword)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        public void MineBlocks(int blockCount, CoreNode node, string accountName, string toWalletName, string withPassword, long expectedFees = 0)
        {
            this.WaitForNodeToSync(node);

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

            this.WaitForNodeToSync(node);

            balanceIncrease.Should().Be(node.GetProofOfWorkRewardForMinedBlocks(blockCount) + expectedFees);
        }

        public void MinePremineBlocks(CoreNode node, string walletName, string walletAccount, string walletPassword)
        {
            this.WaitForNodeToSync(node);

            var unusedAddress = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, walletAccount));
            var wallet = node.FullNode.WalletManager().GetWalletByName(walletName);
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, unusedAddress).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));
            node.GenerateStratisWithMiner(2);

            this.WaitForNodeToSync(node);

            var spendable = node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName);
            var amountShouldBe = node.FullNode.Network.Consensus.Option<PosConsensusOptions>().PremineReward + node.FullNode.Network.Consensus.Option<PosConsensusOptions>().ProofOfWorkReward;
            spendable.Sum(s => s.Transaction.Amount).Should().Be(amountShouldBe);
        }

        public void WaitForNodeToSync(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(n =>
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(n)));

            nodes.Skip(1).ToList().ForEach(
                n => TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodes.First(), n)));
        }
    }
}