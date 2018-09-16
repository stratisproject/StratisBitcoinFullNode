﻿using System.Collections.Generic;
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
using Stratis.Bitcoin.Tests.Common;
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
        private WalletAccountReference sendingWalletAccountReference;
        private Transaction transaction;
        private Key key;
        private uint256 blockWithOpReturnId;

        private readonly string senderWallet = "sender";
        private readonly string receiverWallet = "receiver";
        private readonly string account = "account 0";
        private readonly string password = "p@ssw0rd";
        private readonly string passphrase = "p@ssphr@se";
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
            this.senderNode = this.builder.CreateStratisPowNode(KnownNetworks.RegTest);
            this.receiverNode = this.builder.CreateStratisPowNode(KnownNetworks.RegTest);
            this.builder.StartAll();
            this.senderNode.NotInIBD();
            this.receiverNode.NotInIBD();
        }

        private void a_sending_and_a_receiving_wallet()
        {
            this.receiverNode.FullNode.WalletManager().CreateWallet(this.password, this.receiverWallet, this.passphrase);
            this.receiverAddress = this.receiverNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(this.receiverWallet, this.account));
            this.sendingWallet = this.receiverNode.FullNode.WalletManager().GetWalletByName(this.receiverWallet);

            this.senderNode.FullNode.WalletManager().CreateWallet(this.password, this.senderWallet, this.passphrase);
            this.sendingWalletAccountReference = new WalletAccountReference(this.senderWallet, this.account);
            this.senderAddress = this.senderNode.FullNode.WalletManager().GetUnusedAddress(this.sendingWalletAccountReference);
            this.sendingWallet = this.senderNode.FullNode.WalletManager().GetWalletByName(this.senderWallet);
        }
        private void some_funds_in_the_sending_wallet()
        {
            int maturity = (int)this.senderNode.FullNode.Network.Consensus.CoinbaseMaturity;
            TestHelper.MineBlocks(this.senderNode, this.senderWallet, this.password, this.account, maturity + 5);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.senderNode));

            this.senderNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.senderWallet)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(Money.COIN * 6 * 50);
        }

        private void no_fund_in_the_receiving_wallet()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.receiverWallet)
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
                AccountReference = this.sendingWalletAccountReference,
                MinConfirmations = maturity,
                OpReturnData = this.opReturnContent,
                WalletPassword = this.password,
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
            this.blockWithOpReturnId = TestHelper.MineBlocks(this.senderNode, this.senderWallet, this.password, this.account, 1).BlockHashes.Single();
            TestHelper.MineBlocks(this.senderNode, this.senderWallet, this.password, this.account, 1);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.senderNode));
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.senderNode, this.receiverNode));
        }

        private void the_transaction_should_get_confirmed()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.receiverWallet, 2)
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