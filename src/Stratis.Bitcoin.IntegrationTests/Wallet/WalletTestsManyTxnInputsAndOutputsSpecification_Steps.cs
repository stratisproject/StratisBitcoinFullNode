using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    using Stratis.Bitcoin.Features.Consensus;
    using Stratis.Bitcoin.Features.Wallet;
    using NBitcoin;
    using System.Collections.Generic;
    using System.Linq;
    using Stratis.Bitcoin.Features.Wallet.Controllers;
    using Stratis.Bitcoin.Features.Wallet.Models;
    using System;
    using FluentAssertions;

    public partial class WalletTestsManyTxnInputsAndOutputsSpecification : BddSpecification
    {
        private NodeBuilder nodeBuilder;
        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private CoreNode stratisSender;
        private CoreNode stratisReceiver;
        private long stratisReceiverTotalAmount;
        private Key keySender;
        private TransactionBuildContext transactionBuildContext;
        private int CoinBaseMaturity;

        // Output helper allows test step names to be logged.
        public WalletTestsManyTxnInputsAndOutputsSpecification(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void a_sender_and_receiver_and_their_wallets()
        {
            this.stratisSender = this.nodeBuilder.CreateStratisPowNode();
            this.stratisReceiver = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();
            this.stratisSender.NotInIBD();
            this.stratisReceiver.NotInIBD();

            this.stratisSender.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName);
            this.stratisReceiver.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName);

            HdAddress addrSender = this.stratisSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));
            Wallet walletSender = this.stratisSender.FullNode.WalletManager().GetWalletByName(WalletName);
            this.keySender = walletSender.GetExtendedPrivateKeyForAddress(WalletPassword, addrSender).PrivateKey;
        }

        private void a_block_is_mined()
        {
            this.stratisSender.SetDummyMinerSecret(new BitcoinSecret(this.keySender, this.stratisSender.FullNode.Network));
            this.CoinBaseMaturity = (int)this.stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
            this.stratisSender.GenerateStratisWithMiner(this.CoinBaseMaturity + 51);

            // Wait for block repo for block sync to work.
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.stratisSender));

            this.stratisSender.FullNode.WalletManager().
                GetSpendableTransactionsInWallet("mywallet").
                Sum(s => s.Transaction.Amount).
                Should().Be(Money.COIN * 150 * 50);

            // Sync both nodes.
            this.stratisSender.CreateRPCClient().AddNode(this.stratisReceiver.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.stratisReceiver, this.stratisSender));
        }

        private void funds_are_sent_to_receiver_via_fifty_txn_outputs()
        {
            // Get 50 unused addresses from the receiver.
            IEnumerable<HdAddress> recevierAddresses = this.stratisReceiver.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletName, WalletAccountName), 50);

            List<Recipient> recipients = recevierAddresses.Select(address => new Recipient
            {
                ScriptPubKey = address.ScriptPubKey,
                Amount = Money.COIN
            }).ToList();

            this.transactionBuildContext = new TransactionBuildContext(new WalletAccountReference(WalletName, WalletAccountName), recipients, WalletPassword)
            {
                FeeType = FeeType.Medium,
                MinConfirmations = 101
            };

            Transaction transaction = this.stratisSender.FullNode.WalletTransactionHandler().
                BuildTransaction(this.transactionBuildContext);

            transaction.Outputs.Count.Should().Be(51);

            // Broadcast to the other node.
            this.stratisSender.FullNode.NodeService<WalletController>().
                SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            // Wait for the trx's to arrive.
            TestHelper.WaitLoop(() => this.stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
        }

        private void the_funds_are_received()
        {
            this.stratisReceiverTotalAmount = this.stratisReceiver.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Sum(s => s.Transaction.Amount);

            this.stratisReceiverTotalAmount.Should().Be(5000000000);

            this.stratisReceiver.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                First().Transaction.BlockHeight.
                Should().BeNull();

            this.stratisReceiver.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Count().Should().Be(50);

            // Generate new blocks so the trx is confirmed.
            this.stratisSender.GenerateStratisWithMiner(1);

            // Wait for block repo for block sync to work.
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.stratisSender));
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.stratisReceiver, this.stratisSender));

            // Confirm trx's have been committed to the block.
            this.stratisReceiver.FullNode.WalletManager().
                GetSpendableTransactionsInWallet("mywallet").
                First().Transaction.BlockHeight.
                Should().Be(this.CoinBaseMaturity + 52);       
        }

        private void the_receiver_creates_a_txn_using_fifty_txn_inputs_and_sends_back()
        {
            // Send coins to the receiver.
            var sendto = this.stratisSender.FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            this.transactionBuildContext = new TransactionBuildContext(
                new WalletAccountReference(WalletName, WalletAccountName),
                new[]
                {
                    new Recipient
                    {
                        Amount = this.stratisReceiverTotalAmount - Money.COIN,
                        ScriptPubKey = sendto.ScriptPubKey
                    }
                }.ToList(), WalletPassword);

            // Check receiver has the correct inputs.
            var trx = this.stratisReceiver.FullNode.WalletTransactionHandler()
                .BuildTransaction(this.transactionBuildContext);

            trx.Inputs.Count.Should().Be(50);

            // Broadcast.
            this.stratisReceiver.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(trx.ToHex()));

            // Wait for the trx to arrive.
            TestHelper.WaitLoop(() =>
                this.stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
        }

        private void the_sender_receives_the_funds()
        {
            this.stratisSender.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Sum(s => s.Transaction.Amount).Should().Be(747500000000);

            this.stratisReceiver.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Count().Should().Be(1);

            // wait for block repo for block sync to work.
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.stratisSender));
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.stratisSender, this.stratisReceiver));

            TestHelper.WaitLoop(() => 3 == this.stratisSender.FullNode.WalletManager().
                    GetSpendableTransactionsInWallet(WalletName).
                    First().Transaction.BlockHeight);
        }

        private void sender_sends_funds_with_fifty_txn_outputs()
        {
            a_sender_and_receiver_and_their_wallets();
            a_block_is_mined();
            funds_are_sent_to_receiver_via_fifty_txn_outputs();
            the_funds_are_received();
        }
    }
}
