using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Transactions
{
    public class TransactionsWithNullDataSpecifcations : BddSpecification
    {
        private NodeBuilder builder;
        private CoreNode node;
        private Wallet sendingWallet;
        private HdAddress senderAddress;
        private HdAddress receiverAddress;
        private WalletAccountReference sendingWalletAccountReference;
        private Transaction transaction;

        private readonly string password = "p@ssw0rd";
        private readonly string opReturnContent = "extra informations!";

        protected TransactionsWithNullDataSpecifcations(ITestOutputHelper output) : base(output)
        {
        }
        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.builder?.Dispose();
        }

        [Fact]
        public void A_nulldata_transaction_is_sent_to_the_network()
        {
            this.Given(this.a_proof_of_stake_node_running);
            this.And(this.a_sending_and_a_receiving_wallet);
            this.And(this.some_funds_in_the_sending_wallet);
            this.And(this.a_nulldata_transaction);
            this.When(this.the_transaction_is_broadcasted);
            this.And(this.the_block_is_mined);
            this.Then(this.the_transaction_should_get_confirmed);
            this.And(this.the_transaction_should_appear_in_the_blockchain);
        }

        #region steps
        private void a_proof_of_stake_node_running()
        {
            this.node = this.builder.CreateStratisPosNode();
            this.node.Start();
            this.node.NotInIBD();
        }

        private void a_sending_and_a_receiving_wallet()
        {
            var mnemo = this.node.FullNode.WalletManager().CreateWallet(this.password, "sender");
            this.sendingWalletAccountReference = new WalletAccountReference("sender", "account 0");
            this.senderAddress = this.node.FullNode.WalletManager().GetUnusedAddress(this.sendingWalletAccountReference);
            this.sendingWallet = this.node.FullNode.WalletManager().GetWalletByName("sender");
            
            this.node.FullNode.WalletManager().CreateWallet(this.password, "receiver");
            this.receiverAddress = this.node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("receiver", "account 0"));
            this.sendingWallet = this.node.FullNode.WalletManager().GetWalletByName("receiver");

        }
        private void some_funds_in_the_sending_wallet()
        {
            var key = this.sendingWallet.GetExtendedPrivateKeyForAddress(this.password, this.senderAddress).PrivateKey;
            this.node.SetDummyMinerSecret(new BitcoinSecret(key, this.node.FullNode.Network));
            var maturity = (int)this.node.FullNode.Network.Consensus.Option<PosConsensusOptions>().CoinbaseMaturity;
            this.node.GenerateStratisWithMiner(maturity + 5);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.node));
            this.node.FullNode.WalletManager().GetSpendableTransactionsInWallet("sender")
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(Money.COIN * 105 * 50);
        }

        private void a_nulldata_transaction()
        {
            var transactionBuildContext = new TransactionBuildContext(
                this.sendingWalletAccountReference,
                new List<Recipient>() { new Recipient() {Amount = Money.COIN * 10, ScriptPubKey = this.receiverAddress.ScriptPubKey}},
                this.password, this.opReturnContent) {MinConfirmations = 2};
            this.transaction = this.node.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);

            this.transaction.Outputs.Single(t => t.ScriptPubKey.IsUnspendable).Value.Should().Be(Money.Zero);
        }

        private void the_transaction_is_broadcasted()
        {
            this.node.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }
        private void the_block_is_mined()
        {
            node.GenerateStratisWithMiner(1);
            node.GenerateStratisWithMiner(1);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.node));
        }

        private void the_transaction_should_get_confirmed()
        {
            this.node.FullNode.WalletManager().GetSpendableTransactionsInWallet("receiver", 2)
                .First().Transaction.Amount
                .Should().Be(Money.COIN * 10);
        }

        private void the_transaction_should_appear_in_the_blockchain()
        {
            //I was looking for a way to explore transactions in the blockchain in order to check it contains
            //the nulldata bit with this.opReturnContent
            //this.node.FullNode.NodeService<Pos>()
        }
        #endregion

    }
}
