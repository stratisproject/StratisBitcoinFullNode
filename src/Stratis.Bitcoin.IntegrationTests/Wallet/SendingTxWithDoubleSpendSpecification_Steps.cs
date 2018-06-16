using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTransactionWithDoubleSpend : BddSpecification
    {
        private NodeBuilder builder;
        private CoreNode stratisSender;
        private CoreNode stratisReceiver;
        private Transaction transaction;
        private ErrorResult errorResult;
        private HdAddress receivingAddress;

        public SendingTransactionWithDoubleSpend(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create(this);
            this.stratisSender = this.builder.CreateStratisPowNode();
            this.stratisReceiver = this.builder.CreateStratisPowNode();

            this.builder.StartAll();
            this.stratisSender.NotInIBD();
            this.stratisReceiver.NotInIBD();
        }

        protected override void AfterTest()
        {
            this.builder.Dispose();
        }

        private void wallets_with_coins()
        {
            var mnemonic1 = this.stratisSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
            var mnemonic2 = this.stratisReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
            mnemonic1.Words.Length.Should().Equals(12);
            mnemonic2.Words.Length.Should().Equals(12);

            var addr = this.stratisSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
            var wallet = this.stratisSender.FullNode.WalletManager().GetWalletByName("mywallet");
            var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

            this.stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, this.stratisSender.FullNode.Network));
            var maturity = (int)this.stratisSender.FullNode.Network.Consensus.CoinbaseMaturity;

            this.stratisSender.GenerateStratisWithMiner(maturity + 5);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.stratisSender));

            var total = this.stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
            total.Should().Equals(Money.COIN * 105 * 50);
 
            // sync both nodes
            this.stratisSender.CreateRPCClient().AddNode(stratisReceiver.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
        }

        private void coins_first_sent_to_receiving_wallet()
        {
            this.receivingAddress = this.stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));

            this.transaction = this.stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(WalletTests.CreateContext(
                new WalletAccountReference("mywallet", "account 0"), "123456", this.receivingAddress.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

            this.stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));

            TestHelper.WaitLoop(() => this.stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

            var receivetotal = this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
            receivetotal.Should().Equals(Money.COIN * 100);
            this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight.Should().BeNull();
        }

        private void receiving_node_attempts_to_double_spend_mempool_doesnotaccept()
        {
            //create double spend transaction
            var unusedAddress = this.stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
            var transactionCloned = this.transaction.Clone();
            transactionCloned.Outputs[1].ScriptPubKey = unusedAddress.ScriptPubKey;
            this.stratisReceiver.FullNode.MempoolManager().Validator.AcceptToMemoryPool(new MempoolValidationState(true), transactionCloned).Result.Should().BeFalse();
        }

        private void trx_is_consumed_from_mempool_and_mined_into_a_block()
        {
            List<uint256> mempoolTransactions = this.stratisSender.FullNode.MempoolManager().GetMempoolAsync().Result;
            mempoolTransactions.Should().Contain(this.transaction.GetHash());

            SharedSteps sharedSteps = new SharedSteps();
            new SharedSteps().MineBlocks(1, this.stratisSender, "account 0", "mywallet", "123456", 16360L);
            
            TestHelper.WaitLoop(() => this.stratisSender.FullNode.MempoolManager().GetMempoolAsync().Result.Count==0);
            sharedSteps.WaitForNodeToSync(this.stratisSender, this.stratisReceiver);
            this.stratisReceiver.FullNode.MempoolManager().GetMempoolAsync().Result.Should().NotContain(this.transaction.GetHash());
        }
    }
}
