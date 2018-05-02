using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Transactions
{
    public partial class TransactionWithNullDataSpecification : BddSpecification
    {
        private NodeBuilder builder;
        private CoreNode senderNode;
        private CoreNode receiverNode;
        private Features.Wallet.Wallet sendingWallet;
        private HdAddress senderAddress;
        private HdAddress receiverAddress;
        private WalletAccountReference sendingWalletAccountReference;
        private Transaction transaction;
        private Key key;
        private uint256 blockWithOpReturnId;

        private readonly string password = "p@ssw0rd";
        private readonly string opReturnContent = "extra informations!";
        private readonly int transferAmount = 31415;

        public TransactionWithNullDataSpecification(ITestOutputHelper output) : base(output)
        {
        }
        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create(caller: this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.builder?.Dispose();
        }

        private void two_proof_of_work_nodes()
        {
            this.senderNode = this.builder.CreateStratisPowNode();
            this.receiverNode = this.builder.CreateStratisPowNode();
            this.builder.StartAll();
            this.senderNode.NotInIBD();
            this.receiverNode.NotInIBD();
        }

        private void a_sending_and_a_receiving_wallet()
        {
            this.receiverNode.FullNode.WalletManager().CreateWallet(this.password, "receiver");
            this.receiverAddress = this.receiverNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("receiver", "account 0"));
            this.sendingWallet = this.receiverNode.FullNode.WalletManager().GetWalletByName("receiver");

            this.senderNode.FullNode.WalletManager().CreateWallet(this.password, "sender");
            this.sendingWalletAccountReference = new WalletAccountReference("sender", "account 0");
            this.senderAddress = this.senderNode.FullNode.WalletManager().GetUnusedAddress(this.sendingWalletAccountReference);
            this.sendingWallet = this.senderNode.FullNode.WalletManager().GetWalletByName("sender");
        }
        private void some_funds_in_the_sending_wallet()
        {
            this.key = this.sendingWallet.GetExtendedPrivateKeyForAddress(this.password, this.senderAddress).PrivateKey;
            this.senderNode.SetDummyMinerSecret(new BitcoinSecret(this.key, this.senderNode.FullNode.Network));
            var maturity = (int)this.senderNode.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
            this.senderNode.GenerateStratisWithMiner(maturity + 5);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.senderNode));

            this.senderNode.FullNode.WalletManager().GetSpendableTransactionsInWallet("sender")
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(Money.COIN * 105 * 50);
        }

        private void no_fund_in_the_receiving_wallet()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet("receiver")
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(Money.Zero);
        }

        private void the_wallets_are_in_sync()
        {
            this.senderNode.CreateRPCClient().AddNode(this.receiverNode.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.senderNode, this.receiverNode));
        }

        private void a_nulldata_transaction()
        {
            var transactionBuildContext = new TransactionBuildContext(
                this.sendingWalletAccountReference,
                new List<Recipient>() { new Recipient() { Amount = this.transferAmount, ScriptPubKey = this.receiverAddress.ScriptPubKey } },
                this.password, this.opReturnContent)
            { MinConfirmations = 2 };
            this.transaction = this.senderNode.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);

            this.transaction.Outputs.Single(t => t.ScriptPubKey.IsUnspendable).Value.Should().Be(Money.Zero);
        }

        private void the_transaction_is_broadcasted()
        {
            this.senderNode.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }
        private void the_block_is_mined()
        {
            this.blockWithOpReturnId = this.senderNode.GenerateStratisWithMiner(1).Single();
            this.senderNode.GenerateStratisWithMiner(1);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.senderNode));
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.senderNode, this.receiverNode));
        }

        private void the_transaction_should_get_confirmed()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet("receiver", 2)
                .First().Transaction.Amount.Satoshi
                .Should().Be(this.transferAmount);
        }

        private async Task the_transaction_should_appear_in_the_blockchain()
        {
            var block = await this.senderNode.FullNode.BlockStoreManager().BlockRepository
                    .GetAsync(this.blockWithOpReturnId);

            var transactionFromBlock = block.Transactions
                .Single(t => t.ToHex() == this.transaction.ToHex());

            var opReturnOutputFromBlock = transactionFromBlock.Outputs.Single(t => t.ScriptPubKey.IsUnspendable);
            opReturnOutputFromBlock.Value.Satoshi.Should().Be(0);
            var ops = opReturnOutputFromBlock.ScriptPubKey.ToOps().ToList();
            ops.First().Code.Should().Be(OpcodeType.OP_RETURN);
            ops.Last().PushData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(this.opReturnContent));
        }
    }
}
