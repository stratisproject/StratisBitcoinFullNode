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
        private long stratisReceiverTotalAmount;
        private TransactionBuildContext transactionBuildContext;
        private int CoinBaseMaturity;

        // Output helper allows test step names to be logged.
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
            // Mine block
            this.CoinBaseMaturity = (int)this.nodes[NodeOne].FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.sharedSteps.MineBlocks(this.CoinBaseMaturity + 50
                , this.nodes[NodeOne]
                , WalletAccountName
                , WalletName
                , WalletPassword);

            // Sync both nodes.
            this.nodes[NodeOne].CreateRPCClient().AddNode(this.nodes[NodeTwo].Endpoint, true);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeTwo], this.nodes[NodeOne]);

            // Get 50 unused addresses from the receiver.
            IEnumerable<HdAddress> recevierAddresses = this.nodes[NodeTwo].FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), 50);

            List<Recipient> recipients = recevierAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN
            }).ToList();

            this.transactionBuildContext = SharedSteps.CreateTransactionBuildContext(WalletName
                , WalletAccountName
                , WalletPassword
                , recipients
                , FeeType.Medium
                , 101);

            Transaction transaction = this.nodes[NodeOne].FullNode.WalletTransactionHandler().
                BuildTransaction(this.transactionBuildContext);

            transaction.Outputs.Count.Should().Be(51);

            // Broadcast to the other node.
            this.nodes[NodeOne].FullNode.NodeService<WalletController>().
                SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            // Wait for the transactions to arrive.
            TestHelper.WaitLoop(() => this.nodes[NodeTwo].CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.nodes[NodeTwo].FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
        }

        private void node2_receives_the_funds()
        {
            this.stratisReceiverTotalAmount = this.nodes[NodeTwo].FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Sum(s => s.Transaction.Amount);

            this.stratisReceiverTotalAmount.Should().Be(5000000000);

            this.nodes[NodeTwo].FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                First().Transaction.BlockHeight.
                Should().BeNull();

            this.nodes[NodeTwo].FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Count().Should().Be(50);

            this.nodes[NodeOne].GenerateStratisWithMiner(1);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeOne]);
            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeTwo], this.nodes[NodeOne]);

            // Confirm transactions have been committed to the block.
            this.nodes[NodeTwo].FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                First().Transaction.BlockHeight.
                Should().Be(this.CoinBaseMaturity + 51);
        }

        private void node2_sends_funds_to_node1_FROM_fifty_addresses()
        {
            // Send coins to the receiver.
            var sendto = this.nodes[NodeOne].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            this.transactionBuildContext = new TransactionBuildContext(
                new WalletAccountReference(WalletName, WalletAccountName),
                new[]
                {
                    new Recipient
                    {
                        Amount = this.stratisReceiverTotalAmount - Money.COIN,
                        ScriptPubKey = sendto.ScriptPubKey
                    }
                }.ToList(), WalletPassword);

            // Check receiver has the correct inputs.
            var trx = this.nodes[NodeTwo].FullNode.WalletTransactionHandler()
                        .BuildTransaction(this.transactionBuildContext);

            trx.Inputs.Count.Should().Be(50);

            // Broadcast.
            this.nodes[NodeTwo].FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(trx.ToHex()));

            // Wait for the transaction to arrive.
            TestHelper.WaitLoop(() => this.nodes[NodeOne].FullNode.WalletManager()
                                        .GetSpendableTransactionsInWallet(WalletName).Any());
        }

        private void node1_receives_the_funds()
        {
            this.nodes[NodeOne].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Sum(s => s.Transaction.Amount).Should().Be(745000000000);

            this.nodes[NodeTwo].FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(WalletName)
                .Count().Should().Be(1);

            this.sharedSteps.WaitForBlockStoreToSync(this.nodes[NodeOne]);
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
