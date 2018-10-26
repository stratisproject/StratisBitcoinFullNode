using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.TestNetworks;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingToAndFromManyAddressesSpecification : BddSpecification
    {
        private NodeBuilder nodeBuilder;
        private CoreNode firstNode;
        private CoreNode secondNode;
        private TransactionBuildContext transactionBuildContext;
        private Money firstNodeChangeAmount;
        private Money firstNodeTransactionFee;

        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletAccountName = "account 0";
        private const int UnspentTransactionOutputs = 50;

        public SendingToAndFromManyAddressesSpecification(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create(this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void two_connected_nodes()
        {
            this.firstNode = this.nodeBuilder.CreateStratisPowNode(new BitcoinRegTestOverrideCoinbaseMaturity(1)).WithWallet().Start();
            this.secondNode = this.nodeBuilder.CreateStratisPowNode(new BitcoinRegTestOverrideCoinbaseMaturity(1)).WithWallet().Start();

            TestHelper.Connect(this.firstNode, this.secondNode);
        }

        private void node1_sends_funds_to_node2_TO_fifty_addresses()
        {
            // Node1 balance : 200 mined, 150 spendable
            TestHelper.MineBlocks(this.firstNode, 3 + 1);
            this.firstNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount).Should().Be(Money.Coins(150));
            this.firstNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Count().Should().Be(3);

            IEnumerable<HdAddress> nodeTwoAddresses = this.secondNode.FullNode.WalletManager().GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), 50);
            List<Recipient> nodeTwoRecipients = nodeTwoAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.Coins(1)
            }).ToList();

            // This will create a transaction with an output of a 100 coins as min confirmations is set to 1:
            this.transactionBuildContext = TestHelper.CreateTransactionBuildContext(this.firstNode.FullNode.Network, WalletName, WalletAccountName, WalletPassword, nodeTwoRecipients, FeeType.Medium, 1);
            Transaction transaction = this.firstNode.FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);

            // As only 50 coins will be sent from the 150 available, we need to test the change amount.
            this.firstNodeTransactionFee = this.firstNode.FullNode.WalletTransactionHandler().EstimateFee(this.transactionBuildContext);
            this.firstNodeChangeAmount = transaction.TotalOut - Money.Coins(50);
            this.firstNodeChangeAmount.Should().Be(Money.Coins(150) - Money.Coins(50) - this.firstNodeTransactionFee);
            transaction.TotalOut.Should().Be(Money.Coins(150) - this.firstNodeTransactionFee);

            // Returns 50 outputs, including one extra output for change.
            transaction.Outputs.Count.Should().Be(UnspentTransactionOutputs + 1);

            this.firstNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        private void node2_receives_the_funds()
        {
            TestHelper.WaitLoop(() => this.secondNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

            // Node1 balance : 50 + change amount
            this.firstNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount).Should().Be(this.firstNodeChangeAmount);
            
            // We are at network height 3 and the node only received the funds at block 3
            // so only coins with a depth of 3 minus 2 will be spendable as coinbase maturity is 1
            this.secondNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount).Should().Be(Money.Coins(50));
            this.secondNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Count().Should().Be(UnspentTransactionOutputs);

            TestHelper.MineBlocks(this.secondNode, 1);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.firstNode, this.secondNode));

            // Node2 balance : 50
            this.secondNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount).Should().Be(Money.Coins(50));
        }

        private void node2_sends_funds_to_node1_FROM_fifty_addresses()
        {
            HdAddress sendToNodeOne = this.firstNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            // Send 49 coins to node1:
            this.transactionBuildContext = new TransactionBuildContext(this.firstNode.FullNode.Network)
            {
                AccountReference = new WalletAccountReference(WalletName, WalletAccountName),
                WalletPassword = WalletPassword,
                Recipients = new[] { new Recipient { Amount = Money.Coins(50) - Money.COIN, ScriptPubKey = sendToNodeOne.ScriptPubKey } }.ToList()
            };

            Transaction transaction = this.secondNode.FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);
            transaction.Inputs.Count.Should().Be(50);

            this.secondNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction.ToHex()));
            TestHelper.AreNodesSynced(this.firstNode, this.secondNode);
        }

        private void node1_receives_the_funds()
        {
            Money nodeOneBeforeBalance = this.firstNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName, 1).Sum(t => t.Transaction.Amount);
            nodeOneBeforeBalance.Should().Be(Money.Coins(150) - this.firstNodeTransactionFee);
            TestHelper.MineBlocks(this.secondNode, 1);

            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.firstNode, this.secondNode));
            this.firstNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount).Should().Be(nodeOneBeforeBalance + Money.Coins(49));
            this.secondNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Count().Should().Be(2);
        }

        private void funds_across_fifty_addresses_on_node2_wallet()
        {
            two_connected_nodes();
            node1_sends_funds_to_node2_TO_fifty_addresses();
            node2_receives_the_funds();
        }
    }
}
