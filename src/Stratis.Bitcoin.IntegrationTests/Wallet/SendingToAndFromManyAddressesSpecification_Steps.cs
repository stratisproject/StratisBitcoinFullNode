using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    using Stratis.Bitcoin.Features.Consensus;
    using Stratis.Bitcoin.Features.Wallet;
    using NBitcoin;
    using System.Collections.Generic;
    using System.Linq;
    using Stratis.Bitcoin.Features.Wallet.Controllers;
    using Stratis.Bitcoin.Features.Wallet.Models;
    using FluentAssertions;
    using Stratis.Bitcoin.IntegrationTests.Builders;

    public partial class SendingToAndFromManyAddressesSpecification : BddSpecification
    {
        private SharedSteps sharedSteps;
        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private const string NodeOne = "one";
        private const string NodeTwo = "two";
        private long nodeTwoTotalAmount;
        private TransactionBuildContext transactionBuildContext;
        private int CoinBaseMaturity;
        private Money transactionFee;

        public SendingToAndFromManyAddressesSpecification(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder.Dispose();
        }

        private void two_connected_nodes()
        {
            this.nodes = this.nodeGroupBuilder
                .StratisPowNode(NodeOne).Start().NotInIBD().WithWallet(WalletName, WalletPassword)
                .StratisPowNode(NodeTwo).Start().NotInIBD().WithWallet(WalletName, WalletPassword)
                .WithConnections()
                    .Connect(NodeOne, NodeTwo)
                    .AndNoMoreConnections()
                .Build();
        }

        private void node1_sends_funds_to_node2_TO_fifty_addresses()
        {
            this.CoinBaseMaturity = (int)this.nodes[NodeOne].FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.Mine100Coins();

            this.nodes[NodeOne].CreateRPCClient().AddNode(this.nodes[NodeTwo].Endpoint, true);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeTwo], this.nodes[NodeOne]);

            IEnumerable<HdAddress> nodeTwoAddresses = this.nodes[NodeTwo]
                .FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), 50);

            List<Recipient> nodeTwoRecipients = nodeTwoAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN 
            }).ToList();

            this.transactionBuildContext = SharedSteps.CreateTransactionBuildContext(WalletName
                , WalletAccountName
                , WalletPassword
                , nodeTwoRecipients
                , FeeType.Medium
                , 101);

            Transaction transaction = this.nodes[NodeOne].FullNode.WalletTransactionHandler()
                .BuildTransaction(this.transactionBuildContext);

            // Returns 50 outputs, including one extra output for change.
            transaction.Outputs.Count.Should().Be(51);

            this.nodes[NodeOne].FullNode.NodeService<WalletController>().
                SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        private void Mine100Coins()
        {
            this.sharedSteps.MineBlocks(this.CoinBaseMaturity + 2
                , this.nodes[NodeOne]
                , WalletAccountName
                , WalletName
                , WalletPassword);
        }

        private void node2_receives_the_funds()
        {
            TestHelper.WaitLoop(() => this.nodes[NodeTwo].FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

            this.nodeTwoTotalAmount = this.nodes[NodeTwo].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Sum(s => s.Transaction.Amount);

            this.nodeTwoTotalAmount.Should().Be(5000000000);

            this.nodes[NodeTwo].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .First().Transaction.BlockHeight
                .Should().BeNull();

            this.nodes[NodeTwo].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Count().Should().Be(50);

            this.nodes[NodeOne].GenerateStratisWithMiner(1);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeTwo], this.nodes[NodeOne]);

            this.nodes[NodeTwo].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .First().Transaction.BlockHeight
                .Should().Be(this.CoinBaseMaturity + 3);
        }

        private void node2_sends_funds_to_node1_FROM_fifty_addresses()
        {
            HdAddress sendToNodeOne = this.nodes[NodeOne].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            this.transactionBuildContext = new TransactionBuildContext(
                new WalletAccountReference(WalletName, WalletAccountName),
                new[]
                {
                    new Recipient
                    {
                        Amount = this.nodeTwoTotalAmount - Money.COIN,
                        ScriptPubKey = sendToNodeOne.ScriptPubKey
                    }
                }.ToList(), WalletPassword);

            Transaction transaction = this.nodes[NodeTwo].FullNode.WalletTransactionHandler()
                        .BuildTransaction(this.transactionBuildContext);

            this.transactionFee = this.nodes[NodeTwo].FullNode
                .WalletTransactionHandler()
                .EstimateFee(this.transactionBuildContext);

            transaction.Inputs.Count.Should().Be(50);

            this.nodes[NodeTwo].FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(transaction.ToHex()));
        }

        private void node1_receives_the_funds()
        {
            this.nodes[NodeOne].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Sum(s => s.Transaction.Amount).Should().Be(510000000000);

            this.sharedSteps.MineBlocks(1
                , this.nodes[NodeTwo]
                , WalletAccountName
                , WalletName
                , WalletPassword
                , this.transactionFee.Satoshi);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeTwo], this.nodes[NodeOne]);

            this.nodes[NodeOne].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Sum(s => s.Transaction.Amount).Should().Be(514900000000);

            this.nodes[NodeTwo].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Count().Should().Be(2);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeTwo], this.nodes[NodeOne]);

            TestHelper.WaitLoop(() => 3 == this.nodes[NodeOne].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .First().Transaction.BlockHeight);
        }

        private void funds_across_fifty_addresses_on_node2_wallet()
        {
            two_connected_nodes();
            node1_sends_funds_to_node2_TO_fifty_addresses();
            node2_receives_the_funds();
        }
    }
}
