using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTxWithDoubleSpend : BddSpecification
    {
        private NodeBuilder builder;
        private CoreNode stratisSender;
        private CoreNode stratisReceiver;
        private Transaction transaction;
        private ErrorResult errorResult;
        private HdAddress sendto;

        public SendingTxWithDoubleSpend(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create();
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
            var maturity = (int)this.stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.stratisSender.GenerateStratis(maturity + 5);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.stratisSender));

            var total = this.stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
            total.Should().Equals(Money.COIN * 105 * 50);
 
            // sync both nodes
            this.stratisSender.CreateRPCClient().AddNode(stratisReceiver.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
        }

        private void coins_first_sent_to_receiving_wallet()
        {
            this.sendto = this.stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));

            this.transaction = this.stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(WalletTests.CreateContext(
                new WalletAccountReference("mywallet", "account 0"), "123456", this.sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

            this.stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));

            TestHelper.WaitLoop(() => this.stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

            var receivetotal = this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
            receivetotal.Should().Equals(Money.COIN * 100);
            this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight.Should().BeNull();
        }

        private void attempt_made_to_spend_same_coins()
        {
            //create double spend transaction
            var sendtoDoubleSpend = this.stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
            sendtoDoubleSpend.Should().NotBeSameAs(this.sendto);

            var trxCloned = this.transaction.Clone();
            trxCloned.Outputs[1].ScriptPubKey = sendtoDoubleSpend.ScriptPubKey;

            // broadcast to the other node
            this.errorResult = (ErrorResult)this.stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trxCloned.ToHex()));
        }

        private void mempool_rejects_doublespent_transaction()
        {
            ErrorResponse e = (ErrorResponse)this.errorResult.Value;
            e.Errors[0].Message.Should().BeEquivalentTo("txn-mempool-conflict");  
        }
    }
}
