using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Common;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTransactionOverPolicyByteLimit : BddSpecification
    {
        private SharedSteps sharedSteps;
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private TransactionBuildContext transactionBuildContext;

        private Money transactionFee;
        private Transaction transaction;

        private int CoinBaseMaturity;
        private Exception caughtException;
        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private const string NodeOne = "one";
        private const string NodeTwo = "two";
        
        public SendingTransactionOverPolicyByteLimit(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder(this.CurrentTest.DisplayName);
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

        private void node1_builds_undersize_transaction_to_send_to_node2()
        {
            Node1BuildsTransactionToSendToNode2(2450);
        }

        private void serialized_size_of_transaction_is_close_to_upper_limit()
        {
            this.transaction.GetSerializedSize().Should().BeInRange(95000, 100000);
        }

        private void mempool_of_receiver_node2_is_empty()
        {
            this.nodes[NodeTwo].FullNode.MempoolManager().GetMempoolAsync().Result.Should().BeEmpty();
        }

        private void mempool_of_node2_has_received_transaction()
        {
            TestHelper.WaitLoop(() => this.nodes[NodeOne].FullNode.MempoolManager().GetMempoolAsync().Result.Any());
            this.nodes[NodeOne].FullNode.MempoolManager().GetMempoolAsync().Result.Should().Contain(this.transaction.GetHash());
        }
        
        private void node1_builds_oversize_tx_to_send_to_node2()
        {
            Node1BuildsTransactionToSendToNode2(2900);
            
        }

        private void sending_the_transaction()
        {
            this.nodes[NodeOne].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.transaction.ToHex(this.nodes[NodeOne].FullNode.Network)));
        }

        private void Node1BuildsTransactionToSendToNode2(int txoutputs)
        { 
            this.CoinBaseMaturity = (int) this.nodes[NodeOne].FullNode.Network.Consensus.CoinbaseMaturity;

            this.MineBlocks(this.nodes[NodeOne]);

            var nodeTwoAddresses = this.nodes [NodeTwo].FullNode.WalletManager().GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), txoutputs);

            var nodeTwoRecipients = nodeTwoAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN
                }).ToList();

            this.transactionBuildContext = SharedSteps.CreateTransactionBuildContext(WalletName, WalletAccountName, WalletPassword, nodeTwoRecipients, FeeType.Medium, 101);

            try
            {
                this.transaction = this.nodes[NodeOne].FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);
                this.transactionFee = this.nodes[NodeOne].GetFee(this.transactionBuildContext);
            }
            catch (Exception e)
            {
                this.caughtException = e;
            }
        }

        private void node1_fails_with_oversize_transaction_wallet_error()
        {
            this.caughtException.Should().BeOfType<WalletException>().Which.Message.Should().Contain("Transaction's size is too high");
        }

        private void node1_wallet_throws_no_exceptions()
        {
            this.caughtException.Should().BeNull();
        }

        private void MineBlocks(CoreNode node)
        {
            this.sharedSteps.MineBlocks(this.CoinBaseMaturity * 2 , node, WalletAccountName, WalletName, WalletPassword);
        }
    }
}
