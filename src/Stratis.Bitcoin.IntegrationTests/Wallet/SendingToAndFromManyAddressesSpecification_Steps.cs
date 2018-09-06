using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingToAndFromManyAddressesSpecification : BddSpecification
    {
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private TransactionBuildContext transactionBuildContext;

        private Money nodeTwoBalance;
        private Money transactionFee;

        private int CoinBaseMaturity;

        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletPassphrase = "passphrase";
        private const string WalletAccountName = "account 0";
        private const string NodeOne = "one";
        private const string NodeTwo = "two";
        private const int UnspentTransactionOutputs = 50;

        public SendingToAndFromManyAddressesSpecification(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            KnownNetworks.RegTest.Consensus.CoinbaseMaturity = 1;
            this.CoinBaseMaturity = (int)KnownNetworks.RegTest.Consensus.CoinbaseMaturity;
            this.nodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName), KnownNetworks.RegTest);
        }

        protected override void AfterTest()
        {
            KnownNetworks.RegTest.Consensus.CoinbaseMaturity = 100;
            this.nodeGroupBuilder.Dispose();
        }

        private void two_connected_nodes()
        {
            this.nodes = this.nodeGroupBuilder
                .StratisPowNode(NodeOne).Start().NotInIBD().WithWallet(WalletName, WalletPassword, WalletPassphrase)
                .StratisPowNode(NodeTwo).Start().NotInIBD().WithWallet(WalletName, WalletPassword, WalletPassphrase)
                .WithConnections()
                    .Connect(NodeOne, NodeTwo)
                    .AndNoMoreConnections()
                .Build();
        }

        private void node1_sends_funds_to_node2_TO_fifty_addresses()
        {
            TestHelper.MineBlocks(this.nodes[NodeOne], WalletName, WalletPassword, WalletAccountName, (uint)this.CoinBaseMaturity + 2);

            IEnumerable<HdAddress> nodeTwoAddresses = this.nodes[NodeTwo].FullNode.WalletManager().GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), 50);

            List<Recipient> nodeTwoRecipients = nodeTwoAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN
            }).ToList();

            this.transactionBuildContext = TestHelper.CreateTransactionBuildContext(this.nodes[NodeOne].FullNode.Network, WalletName, WalletAccountName, WalletPassword, nodeTwoRecipients, FeeType.Medium, this.CoinBaseMaturity + 1);

            Transaction transaction = this.nodes[NodeOne].FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);

            // Returns 50 outputs, including one extra output for change.
            transaction.Outputs.Count.Should().Be(UnspentTransactionOutputs + 1);

            this.transactionFee = this.nodes[NodeOne].GetFee(this.transactionBuildContext);

            this.nodes[NodeOne].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        private void node2_receives_the_funds()
        {
            TestHelper.WaitLoop(() => this.nodes[NodeTwo].FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

            this.nodeTwoBalance = this.nodes[NodeTwo].WalletBalance(WalletName);

            this.nodeTwoBalance.Should().Be(Money.Coins(50));

            this.nodes[NodeTwo].WalletHeight(WalletName).Should().BeNull();

            this.nodes[NodeTwo].WalletSpendableTransactionCount(WalletName).Should().Be(UnspentTransactionOutputs);

            TestHelper.MineBlocks(this.nodes[NodeTwo], WalletName, WalletPassword, WalletAccountName, 1);

            this.nodes[NodeTwo].WalletHeight(WalletName).Should().Be(this.CoinBaseMaturity + 3);
        }

        private void node2_sends_funds_to_node1_FROM_fifty_addresses()
        {
            HdAddress sendToNodeOne = this.nodes[NodeOne].FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            this.transactionBuildContext = new TransactionBuildContext(this.nodes[NodeOne].FullNode.Network)
            {
                AccountReference = new WalletAccountReference(WalletName, WalletAccountName),
                WalletPassword = WalletPassword,
                Recipients = new[] { new Recipient { Amount = this.nodeTwoBalance - Money.COIN, ScriptPubKey = sendToNodeOne.ScriptPubKey } }.ToList()
            };

            Transaction transaction = this.nodes[NodeTwo].FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);

            this.transactionFee = this.nodes[NodeTwo].GetFee(this.transactionBuildContext);

            transaction.Inputs.Count.Should().Be(50);

            this.nodes[NodeTwo].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
            TestHelper.AreNodesSynced(this.nodes[NodeOne], this.nodes[NodeTwo]);
        }

        private void node1_receives_the_funds()
        {
            Money nodeOneBeforeBalance = this.nodes[NodeOne].WalletBalance(WalletName);

            TestHelper.MineBlocks(this.nodes[NodeTwo], WalletName, WalletPassword, WalletAccountName, 1);

            this.nodes[NodeOne].WalletBalance(WalletName).Should().Be(nodeOneBeforeBalance + Money.Coins(49));

            this.nodes[NodeTwo].WalletSpendableTransactionCount(WalletName).Should().Be(3);
        }

        private void funds_across_fifty_addresses_on_node2_wallet()
        {
            two_connected_nodes();
            node1_sends_funds_to_node2_TO_fifty_addresses();
            node2_receives_the_funds();
        }
    }
}
