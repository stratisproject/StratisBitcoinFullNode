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

    public partial class SendingToAndFromManyAddressesSpecification : BddSpecification
    {
        private NodeBuilder nodeBuilder;
        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private CoreNode nodeOne;
        private CoreNode nodeTwo;
        private long stratisReceiverTotalAmount;
        private Key keySender;
        private TransactionBuildContext transactionBuildContext;
        private int CoinBaseMaturity;

        // Output helper allows test step names to be logged.
        public SendingToAndFromManyAddressesSpecification(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void funds_in_a_single_address_on_node1_wallet()
        {
            this.nodeOne = this.nodeBuilder.CreateStratisPowNode();
            this.nodeTwo = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();
            this.nodeOne.NotInIBD();
            this.nodeTwo.NotInIBD();

            this.nodeOne.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName);
            this.nodeTwo.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName);

            HdAddress addrSender = this.nodeOne.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));
            Wallet walletSender = this.nodeOne.FullNode.WalletManager().GetWalletByName(WalletName);
            this.keySender = walletSender.GetExtendedPrivateKeyForAddress(WalletPassword, addrSender).PrivateKey;
        }

        private void node1_sends_funds_to_node2_TO_fifty_addresses()
        {
            // Mine block
            this.nodeOne.SetDummyMinerSecret(new BitcoinSecret(this.keySender, this.nodeOne.FullNode.Network));
            this.CoinBaseMaturity = (int)this.nodeOne.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
            this.nodeOne.GenerateStratisWithMiner(this.CoinBaseMaturity + 51);

            // Wait for block repo for block sync to work.
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodeOne));

            this.nodeOne.FullNode.WalletManager().
                GetSpendableTransactionsInWallet("mywallet").
                Sum(s => s.Transaction.Amount).
                Should().Be(Money.COIN * 150 * 50);

            // Sync both nodes.
            this.nodeOne.CreateRPCClient().AddNode(this.nodeTwo.Endpoint, true);
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.nodeTwo, this.nodeOne));

            // Get 50 unused addresses from the receiver.
            IEnumerable<HdAddress> recevierAddresses = this.nodeTwo.FullNode.WalletManager()
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

            Transaction transaction = this.nodeOne.FullNode.WalletTransactionHandler().
                BuildTransaction(this.transactionBuildContext);

            transaction.Outputs.Count.Should().Be(51);

            // Broadcast to the other node.
            this.nodeOne.FullNode.NodeService<WalletController>().
                SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            // Wait for the trx's to arrive.
            TestHelper.WaitLoop(() => this.nodeTwo.CreateRPCClient().GetRawMempool().Length > 0);
            TestHelper.WaitLoop(() => this.nodeTwo.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
        }

        private void node2_receives_the_funds()
        {
            this.stratisReceiverTotalAmount = this.nodeTwo.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Sum(s => s.Transaction.Amount);

            this.stratisReceiverTotalAmount.Should().Be(5000000000);

            this.nodeTwo.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                First().Transaction.BlockHeight.
                Should().BeNull();

            this.nodeTwo.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Count().Should().Be(50);

            // Generate new blocks so the trx is confirmed.
            this.nodeOne.GenerateStratisWithMiner(1);

            // Wait for block repo for block sync to work.
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodeOne));
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.nodeTwo, this.nodeOne));

            // Confirm trx's have been committed to the block.
            this.nodeTwo.FullNode.WalletManager().
                GetSpendableTransactionsInWallet("mywallet").
                First().Transaction.BlockHeight.
                Should().Be(this.CoinBaseMaturity + 52);       
        }

        private void node2_sends_funds_to_node1_FROM_fifty_addresses()
        {
            // Send coins to the receiver.
            var sendto = this.nodeOne.FullNode.WalletManager()
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
            var trx = this.nodeTwo.FullNode.WalletTransactionHandler()
                .BuildTransaction(this.transactionBuildContext);

            trx.Inputs.Count.Should().Be(50);

            // Broadcast.
            this.nodeTwo.FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(trx.ToHex()));

            // Wait for the trx to arrive.
            TestHelper.WaitLoop(() =>
                this.nodeOne.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
        }

        private void node1_receives_the_funds()
        {
            this.nodeOne.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Sum(s => s.Transaction.Amount).Should().Be(747500000000);

            this.nodeTwo.FullNode.WalletManager().
                GetSpendableTransactionsInWallet(WalletName).
                Count().Should().Be(1);

            // wait for block repo for block sync to work.
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodeOne));
            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.nodeOne, this.nodeTwo));

            TestHelper.WaitLoop(() => 3 == this.nodeOne.FullNode.WalletManager().
                    GetSpendableTransactionsInWallet(WalletName).
                    First().Transaction.BlockHeight);
        }

        private void funds_across_fifty_addresses_on_node2_wallet()
        {
            funds_in_a_single_address_on_node1_wallet();
            node1_sends_funds_to_node2_TO_fifty_addresses();
            node2_receives_the_funds();
        }
    }
}
