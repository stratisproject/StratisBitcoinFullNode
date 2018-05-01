using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpecification
    {
        private const string SendingWalletName = "sending wallet";
        private const string ReceivingWalletName = "receiving wallet";
        private const string WalletPassword = "123456";
        private const string AccountName = "account 0";
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;
        private SharedSteps sharedSteps;
        private NodeGroupBuilder nodeGroupBuilder;

        public ProofOfWorkSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeGroupBuilder = new NodeGroupBuilder(this.CurrentTest.DisplayName);
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder.Dispose();
        }

        private void a_sending_and_receiving_stratis_bitcoin_node_and_wallet()
        {
            var nodeGroup = this.nodeGroupBuilder
                .StratisPowNode("sending").Start().NotInIBD()
                .WithWallet(SendingWalletName, WalletPassword)
                .StratisPowNode("receiving").Start().NotInIBD()
                .WithWallet(ReceivingWalletName, WalletPassword)
                .WithConnections()
                .Connect("sending", "receiving")
                .AndNoMoreConnections()
                .Build();

            this.sendingStratisBitcoinNode = nodeGroup["sending"];
            this.receivingStratisBitcoinNode = nodeGroup["receiving"];

            this.coinbaseMaturity = (int)this.sendingStratisBitcoinNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
        }

        private void a_block_is_mined_creating_spendable_coins()
        {
            this.sharedSteps.MineBlocks(1, this.sendingStratisBitcoinNode, AccountName, SendingWalletName, WalletPassword);
        }

        private void more_blocks_mined_to_just_BEFORE_maturity_of_original_block()
        {
            this.sharedSteps.MineBlocks(this.coinbaseMaturity - 1, this.sendingStratisBitcoinNode, AccountName, SendingWalletName, WalletPassword);
        }

        private void more_blocks_mined_to_just_AFTER_maturity_of_original_block()
        {
            this.sharedSteps.MineBlocks(this.coinbaseMaturity, this.sendingStratisBitcoinNode, AccountName, SendingWalletName, WalletPassword);

        }

        private void spending_the_coins_from_original_block()
        {
            var sendtoAddress = this.receivingStratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(ReceivingWalletName, AccountName), 2).ElementAt(1);

            try
            {
                var transactionBuildContext = SharedSteps.CreateTransactionBuildContext(
                    SendingWalletName,
                    AccountName,
                    WalletPassword,
                    new[] {
                        new Recipient {
                            Amount = Money.COIN * 1,
                            ScriptPubKey = sendtoAddress.ScriptPubKey
                        }
                    },
                    FeeType.Medium, 101);

                this.lastTransaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler()
                    .BuildTransaction(transactionBuildContext);

                this.sendingStratisBitcoinNode.FullNode.NodeService<WalletController>()
                    .SendTransaction(new SendTransactionRequest(this.lastTransaction.ToHex()));
            }
            catch (Exception exception)
            {
                this.caughtException = exception;
            }
        }

        private void the_transaction_is_rejected_from_the_mempool()
        {
            this.caughtException.Should().BeOfType<WalletException>();

            var walletException = (WalletException)this.caughtException;
            walletException.Message.Should().Be("No spendable transactions found.");

            this.ResetCaughtException();
        }

        private void the_transaction_is_put_in_the_mempool()
        {
            var tx = this.sendingStratisBitcoinNode.FullNode.MempoolManager().GetTransaction(this.lastTransaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(this.lastTransaction.GetHash());
            this.caughtException.Should().BeNull();
        }

        private void ResetCaughtException()
        {
            this.caughtException = null;
        }
    }
}