using System;
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
    public partial class SendingTransactionOverPolicyByteLimit : BddSpecification
    {
        private SharedSteps sharedSteps;
        private NodeBuilder nodeBuilder;
        private Network network;
        private CoreNode firstNode;
        private CoreNode secondNode;
        private TransactionBuildContext transactionBuildContext;

        private Transaction transaction;

        private int CoinBaseMaturity;
        private Exception caughtException;
        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletPassphrase = "passphrase";
        private const string WalletAccountName = "account 0";

        public SendingTransactionOverPolicyByteLimit(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.network = KnownNetworks.RegTest;
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void two_connected_nodes()
        {
            this.firstNode = this.nodeBuilder.CreateStratisPowNode(this.network);
            this.firstNode.Start();
            this.firstNode.NotInIBD();
            this.firstNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName, WalletPassphrase);

            this.secondNode = this.nodeBuilder.CreateStratisPowNode(this.network);
            this.secondNode.Start();
            this.secondNode.NotInIBD();
            this.secondNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName, WalletPassphrase);

            TestHelper.ConnectAndSync(this.firstNode, this.secondNode);
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
            this.secondNode.FullNode.MempoolManager().GetMempoolAsync().Result.Should().BeEmpty();
        }

        private void mempool_of_node2_has_received_transaction()
        {
            TestHelper.WaitLoop(() => this.firstNode.FullNode.MempoolManager().GetMempoolAsync().Result.Any());
            this.firstNode.FullNode.MempoolManager().GetMempoolAsync().Result.Should().Contain(this.transaction.GetHash());
        }

        private void node1_builds_oversize_tx_to_send_to_node2()
        {
            Node1BuildsTransactionToSendToNode2(2900);

        }

        private void sending_the_transaction()
        {
            this.firstNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.transaction.ToHex(this.firstNode.FullNode.Network)));
        }

        private void Node1BuildsTransactionToSendToNode2(int txoutputs)
        {
            this.CoinBaseMaturity = (int)this.firstNode.FullNode.Network.Consensus.CoinbaseMaturity;

            this.MineBlocks(this.firstNode);

            var nodeTwoAddresses = this.secondNode.FullNode.WalletManager().GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), txoutputs);

            var nodeTwoRecipients = nodeTwoAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN
            }).ToList();

            this.transactionBuildContext = SharedSteps.CreateTransactionBuildContext(this.firstNode.FullNode.Network, WalletName, WalletAccountName, WalletPassword, nodeTwoRecipients, FeeType.Medium, 101);

            try
            {
                this.transaction = this.firstNode.FullNode.WalletTransactionHandler().BuildTransaction(this.transactionBuildContext);
                Money transactionFee = this.firstNode.GetFee(this.transactionBuildContext);
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
            this.sharedSteps.MineBlocks(this.CoinBaseMaturity * 2, node, WalletAccountName, WalletName, WalletPassword);
        }
    }
}
