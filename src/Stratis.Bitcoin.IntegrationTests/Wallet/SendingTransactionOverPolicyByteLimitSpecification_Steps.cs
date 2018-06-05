using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
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
            Node1BuildsTransactionToSendToNode2(2700);
        }

        private void serialized_size_of_transaction_is_within_1KB_of_upper_limit()
        {
            this.transaction.GetSerializedSize().Should().BeInRange(99000, 100000);
        }

        private void node1_builds_oversize_tx_to_send_to_node2()
        {
            Node1BuildsTransactionToSendToNode2(2900);
            
        }

        private void sending_the_transaction()
        {
            this.nodes[NodeOne].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }

        private void Node1BuildsTransactionToSendToNode2(int txoutputs)
        { 
            this.CoinBaseMaturity = (int) this.nodes[NodeOne].FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

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

        private void node1_succeeds_sending_tx_to_node2()
        {
            this.caughtException.Should().BeNull();
        }

        private void MineBlocks(CoreNode node)
        {
            this.sharedSteps.MineBlocks(this.CoinBaseMaturity * 2, node, WalletAccountName, WalletName, WalletPassword);
        }
    }
}
