using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingToAndFromManyAddressesSpecification : BddSpecification
    {

        private NodeBuilder nodeBuilder;
        private Network network;
        private CoreNode firstNode;
        private CoreNode secondNode;
        private TransactionBuildContext transactionBuildContext;
        private Money nodeTwoBalance;
        private int CoinBaseMaturity;

        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";
        private const string WalletAccountName = "account 0";
        private const int UnspentTransactionOutputs = 50;

        public SendingToAndFromManyAddressesSpecification(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            KnownNetworks.RegTest.Consensus.CoinbaseMaturity = 1;
            this.CoinBaseMaturity = (int)KnownNetworks.RegTest.Consensus.CoinbaseMaturity;

            this.network = KnownNetworks.RegTest;
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            KnownNetworks.RegTest.Consensus.CoinbaseMaturity = 100;
            this.nodeBuilder.Dispose();
        }

        private void two_connected_nodes()
        {
            this.firstNode = this.nodeBuilder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();
            this.firstNode.Start();

            this.secondNode = this.nodeBuilder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();
            this.secondNode.Start();

            TestHelper.Connect(this.firstNode, this.secondNode);
        }

        private void node1_sends_funds_to_node2_TO_fifty_addresses()
        {
            TestHelper.MineBlocks(this.firstNode, this.CoinBaseMaturity + 2);

            IEnumerable<HdAddress> nodeTwoAddresses = this.secondNode.FullNode.WalletManager().GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), 50);

            List<Recipient> nodeTwoRecipients = nodeTwoAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN
            }).ToList();

            this.transactionBuildContext = TestHelper.CreateTransactionBuildContext(this.firstNode.FullNode.Network, WalletName, WalletAccountName, WalletPassword, nodeTwoRecipients, FeeType.Medium, this.CoinBaseMaturity + 1);

            Transaction transaction = this.firstNode.FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);

            // Returns 50 outputs, including one extra output for change.
            transaction.Outputs.Count.Should().Be(UnspentTransactionOutputs + 1);

            Money transactionFee = this.firstNode.GetFee(this.transactionBuildContext);

            this.firstNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        private void node2_receives_the_funds()
        {
            TestHelper.WaitLoop(() => this.secondNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

            this.nodeTwoBalance = this.secondNode.WalletBalance(WalletName);

            this.nodeTwoBalance.Should().Be(Money.Coins(50));

            this.secondNode.WalletHeight(WalletName).Should().BeNull();

            this.secondNode.WalletSpendableTransactionCount(WalletName).Should().Be(UnspentTransactionOutputs);

            TestHelper.MineBlocks(this.secondNode, 1);

            this.secondNode.WalletHeight(WalletName).Should().Be(this.CoinBaseMaturity + 3);
        }

        private void node2_sends_funds_to_node1_FROM_fifty_addresses()
        {
            HdAddress sendToNodeOne = this.firstNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            this.transactionBuildContext = new TransactionBuildContext(this.firstNode.FullNode.Network)
            {
                AccountReference = new WalletAccountReference(WalletName, WalletAccountName),
                WalletPassword = WalletPassword,
                Recipients = new[] { new Recipient { Amount = this.nodeTwoBalance - Money.COIN, ScriptPubKey = sendToNodeOne.ScriptPubKey } }.ToList()
            };

            Transaction transaction = this.secondNode.FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);

            Money transactionFee = this.secondNode.GetFee(this.transactionBuildContext);

            transaction.Inputs.Count.Should().Be(50);

            this.secondNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
            TestHelper.AreNodesSynced(this.firstNode, this.secondNode);
        }

        private void node1_receives_the_funds()
        {
            Money nodeOneBeforeBalance = this.firstNode.WalletBalance(WalletName);

            TestHelper.MineBlocks(this.secondNode, 1);

            this.firstNode.WalletBalance(WalletName).Should().Be(nodeOneBeforeBalance + Money.Coins(49));

            this.secondNode.WalletSpendableTransactionCount(WalletName).Should().Be(3);
        }

        private void funds_across_fifty_addresses_on_node2_wallet()
        {
            two_connected_nodes();
            node1_sends_funds_to_node2_TO_fifty_addresses();
            node2_receives_the_funds();
        }
    }
}
