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
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingTransactionWithDoubleSpend : BddSpecification
    {
        private const string Password = "password";
        private const string Name = "mywallet";
        private const string Passphrase = "passphrase";
        private const string AccountName = "account 0";

        private NodeBuilder builder;
        private Network network;
        private CoreNode stratisSender;
        private CoreNode stratisReceiver;
        private Transaction transaction;
        private MempoolValidationState mempoolValidationState;
        private HdAddress receivingAddress;

        public SendingTransactionWithDoubleSpend(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.builder = NodeBuilder.Create(this);
            this.network = new BitcoinRegTest();

            this.stratisSender = this.builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Miner).Start();
            this.stratisReceiver = this.builder.CreateStratisPowNode(this.network).WithWallet().Start();
            this.mempoolValidationState = new MempoolValidationState(true);
        }

        protected override void AfterTest()
        {
            this.builder.Dispose();
        }

        private void wallets_with_coins()
        {
            var maturity = (int)this.stratisSender.FullNode.Network.Consensus.CoinbaseMaturity;
            TestHelper.MineBlocks(this.stratisSender, 5);

            var total = this.stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(Name).Sum(s => s.Transaction.Amount);
            total.Should().Equals(Money.COIN * 6 * 50);

            TestHelper.ConnectAndSync(this.stratisSender, this.stratisReceiver);
        }

        private void coins_first_sent_to_receiving_wallet()
        {
            this.receivingAddress = this.stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(Name, AccountName));

            this.transaction = this.stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(WalletTests.CreateContext(this.stratisSender.FullNode.Network,
                new WalletAccountReference(Name, AccountName), Password, this.receivingAddress.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

            this.stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));

            TestHelper.WaitLoop(() => this.stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(Name).Any());

            var receivetotal = this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(Name).Sum(s => s.Transaction.Amount);
            receivetotal.Should().Equals(Money.COIN * 100);
            this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(Name).First().Transaction.BlockHeight.Should().BeNull();
        }

        private void txn_mempool_conflict_error_occurs()
        {
            this.mempoolValidationState.Error.Code.Should().BeEquivalentTo("txn-mempool-conflict");
        }

        private void receiving_node_attempts_to_double_spend_mempool_doesnotaccept()
        {
            var unusedAddress = this.stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(Name, AccountName));
            var transactionCloned = this.stratisReceiver.FullNode.Network.CreateTransaction(this.transaction.ToBytes());
            transactionCloned.Outputs[1].ScriptPubKey = unusedAddress.ScriptPubKey;
            this.stratisReceiver.FullNode.MempoolManager().Validator.AcceptToMemoryPool(this.mempoolValidationState, transactionCloned).Result.Should().BeFalse();
        }

        private void trx_is_mined_into_a_block_and_removed_from_mempools()
        {
            TestHelper.MineBlocks(this.stratisSender, 1);
            TestHelper.WaitForNodeToSync(this.stratisSender, this.stratisReceiver);

            this.stratisSender.FullNode.MempoolManager().GetMempoolAsync().Result.Should().NotContain(this.transaction.GetHash());
            this.stratisReceiver.FullNode.MempoolManager().GetMempoolAsync().Result.Should().NotContain(this.transaction.GetHash());
        }

        private void trx_is_propagated_across_sending_and_receiving_mempools()
        {
            List<uint256> senderMempoolTransactions = this.stratisSender.FullNode.MempoolManager().GetMempoolAsync().Result;
            senderMempoolTransactions.Should().Contain(this.transaction.GetHash());

            List<uint256> receiverMempoolTransactions = this.stratisSender.FullNode.MempoolManager().GetMempoolAsync().Result;
            receiverMempoolTransactions.Should().Contain(this.transaction.GetHash());
        }
    }
}