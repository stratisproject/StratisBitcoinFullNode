﻿using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    using System.Threading.Tasks;

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
            this.WaitForNodesToSync(node);

            HdAddress address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(toWalletName, accountName));

            long balanceBeforeMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Where(x => x.Address == address)
                .Sum(s => s.Transaction.Amount);

            Wallet wallet = node.FullNode.WalletManager().GetWalletByName(toWalletName);
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(withPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);

            long balanceAfterMining = node.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(toWalletName)
                .Where(x => x.Address == address)
                .Sum(s => s.Transaction.Amount);

            long balanceIncrease = balanceAfterMining - balanceBeforeMining;

            this.WaitForNodesToSync(node);

            balanceIncrease.Should().Be(node.GetProofOfWorkRewardForMinedBlocks(blockCount) + expectedFees);
        }

        public void MinePremineBlocks(CoreNode node, string walletName, string walletAccount, string walletPassword)
        {
            this.WaitForNodesToSync(node);

            HdAddress unusedAddress = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, walletAccount));
            Wallet wallet = node.FullNode.WalletManager().GetWalletByName(walletName);
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, unusedAddress).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));
            node.GenerateStratisWithMiner(2);

            this.WaitForNodesToSync(node);

            IEnumerable<UnspentOutputReference> spendable = node.FullNode.WalletManager().GetSpendableTransactionsInWallet(walletName);
            Money amountShouldBe = node.FullNode.Network.Consensus.PremineReward + node.FullNode.Network.Consensus.ProofOfWorkReward;
            spendable.Sum(s => s.Transaction.Amount).Should().Be(amountShouldBe);
        }

        public void WaitForNodesToSync(params CoreNode[] nodes)
        {
            var firstNode = nodes.First();

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(firstNode));

            nodes.Skip(1).ToList().ForEach(
                n => TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(firstNode, n)));
        }

        //public async Task WaitForNodesToSync(params CoreNode[] nodes)
        //{
        //    var firstNode = nodes.First();

        //    await TestHelper.WaitLoopAsync(() => TestHelper.IsNodeSynced(firstNode));

        //    nodes.Skip(1).ToList().ForEach(
        //        async n => await TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(firstNode, n)));
        //}
    }
}