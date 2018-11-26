using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common.TestFramework;
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
        private Transaction transaction;
        private uint256 blockWithOpReturnId;

        private readonly string walletAccount = "account 0";
        private readonly string walletName = "mywallet";
        private readonly string walletPassword = "password";
        private readonly string opReturnContent = "extra informations!";
        private readonly int transferAmount = 31415;

        public TransactionWithNullDataSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            this.builder?.Dispose();
        }

        private void two_proof_of_work_nodes()
        {
            this.senderNode = this.builder.CreateStratisPowNode(new BitcoinRegTest()).WithWallet().Start();
            this.receiverNode = this.builder.CreateStratisPowNode(new BitcoinRegTest()).WithWallet().Start();
        }

        private void a_sending_and_a_receiving_wallet()
        {
            this.receiverAddress = this.receiverNode.FullNode.WalletManager().GetUnusedAddress();
            this.sendingWallet = this.receiverNode.FullNode.WalletManager().GetWalletByName(this.walletName);

            this.senderAddress = this.senderNode.FullNode.WalletManager().GetUnusedAddress();
            this.sendingWallet = this.senderNode.FullNode.WalletManager().GetWalletByName(this.walletName);
        }

        private void some_funds_in_the_sending_wallet()
        {
            int maturity = (int)this.senderNode.FullNode.Network.Consensus.CoinbaseMaturity;
            TestHelper.MineBlocks(this.senderNode, maturity + 1 + 5);

            this.senderNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.walletName)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(Money.COIN * 6 * 50);
        }

        private void no_fund_in_the_receiving_wallet()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.walletName)
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
            var maturity = (int)this.senderNode.FullNode.Network.Consensus.CoinbaseMaturity;
            var transactionBuildContext = new TransactionBuildContext(this.senderNode.FullNode.Network)
            {
                AccountReference = new WalletAccountReference(this.walletName, this.walletAccount),
                MinConfirmations = maturity,
                OpReturnData = this.opReturnContent,
                WalletPassword = this.walletPassword,
                Recipients = new List<Recipient>() { new Recipient() { Amount = this.transferAmount, ScriptPubKey = this.receiverAddress.ScriptPubKey } }
            };

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
            this.blockWithOpReturnId = TestHelper.MineBlocks(this.senderNode, 2).BlockHashes[0];

            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.senderNode, this.receiverNode));
        }

        private void the_transaction_should_get_confirmed()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.walletName, 2)
                .First().Transaction.Amount.Satoshi
                .Should().Be(this.transferAmount);
        }

        private async Task the_transaction_should_appear_in_the_blockchain()
        {
            Block block = await this.senderNode.FullNode.BlockStore().GetBlockAsync(this.blockWithOpReturnId);

            Transaction transactionFromBlock = block.Transactions
                .Single(t => t.ToHex() == this.transaction.ToHex());

            TxOut opReturnOutputFromBlock = transactionFromBlock.Outputs.Single(t => t.ScriptPubKey.IsUnspendable);
            opReturnOutputFromBlock.Value.Satoshi.Should().Be(0);
            List<Op> ops = opReturnOutputFromBlock.ScriptPubKey.ToOps().ToList();
            ops.First().Code.Should().Be(OpcodeType.OP_RETURN);
            ops.Last().PushData.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(this.opReturnContent));
        }
    }
}