using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public class SharedSteps
    {
        public static TransactionBuildContext CreateTransactionBuildContext(
            Network network,
            string sendingWalletName,
            string sendingAccountName,
            string sendingPassword,
            ICollection<Recipient> recipients,
            FeeType feeType,
            int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = new WalletAccountReference(sendingWalletName, sendingAccountName),
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = sendingPassword,
                Recipients = recipients.ToList()
            };
        }

        public void MineBlocks(int blockCount, CoreNode node, string accountName, string toWalletName, string withPassword)
        {
            this.WaitForNodeToSync(node);

            HdAddress address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(toWalletName, accountName));

            Wallet wallet = node.FullNode.WalletManager().GetWalletByName(toWalletName);
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(withPassword, address).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));

            node.GenerateStratisWithMiner(blockCount);

            this.WaitForNodeToSync(node);
        }

        public void MinePremineBlocks(CoreNode node, string walletName, string walletAccount, string walletPassword)
        {
            this.WaitForNodeToSync(node);

            HdAddress unusedAddress = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, walletAccount));
            Wallet wallet = node.FullNode.WalletManager().GetWalletByName(walletName);
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, unusedAddress).PrivateKey;

            node.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, node.FullNode.Network));
            node.GenerateStratisWithMiner(2);

            this.WaitForNodeToSync(node);

            // Since the premine will not be immediately spendable, the transactions have to be counted directly from the address.
            unusedAddress.Transactions.Count().Should().Be(2);

            Money amountShouldBe = node.FullNode.Network.Consensus.PremineReward + node.FullNode.Network.Consensus.ProofOfWorkReward;

            unusedAddress.Transactions.Sum(s => s.Amount).Should().Be(amountShouldBe);
        }

        public void WaitForNodeToSync(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(node => TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node)));
            nodes.Skip(1).ToList().ForEach(node => TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodes.First(), node)));
        }

        public void WaitForNodeToSyncIgnoreMempool(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(node => TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node)));
            nodes.Skip(1).ToList().ForEach(node => TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodes.First(), node, true)));
        }
    }
}