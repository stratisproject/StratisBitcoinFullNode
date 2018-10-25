using System;
using System.IO;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpecification
    {
        private const string WalletAccountName = "account 0";
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";

        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;

        private NodeBuilder nodeBuilder;
        private Network network;

        public ProofOfWorkSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
            this.network = new BitcoinRegTest();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        protected void a_sending_and_receiving_stratis_bitcoin_node_and_wallet()
        {
            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();
            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();

            TestHelper.Connect(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);

            this.coinbaseMaturity = (int)this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity;
        }

        protected void a_block_is_mined_creating_spendable_coins()
        {
            TestHelper.MineBlocks(this.sendingStratisBitcoinNode, 1);
        }

        private void more_blocks_mined_to_just_BEFORE_maturity_of_original_block()
        {
            TestHelper.MineBlocks(this.sendingStratisBitcoinNode, this.coinbaseMaturity - 1);
        }

        protected void more_blocks_mined_to_just_AFTER_maturity_of_original_block()
        {
            TestHelper.MineBlocks(this.sendingStratisBitcoinNode, this.coinbaseMaturity);
        }

        private void spending_the_coins_from_original_block()
        {
            HdAddress sendtoAddress = this.receivingStratisBitcoinNode.FullNode.WalletManager().GetUnusedAddress();

            try
            {
                TransactionBuildContext transactionBuildContext = TestHelper.CreateTransactionBuildContext(
                    this.sendingStratisBitcoinNode.FullNode.Network,
                    WalletName,
                    WalletAccountName,
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
            Transaction tx = this.sendingStratisBitcoinNode.FullNode.MempoolManager().GetTransaction(this.lastTransaction.GetHash()).GetAwaiter().GetResult();
            tx.GetHash().Should().Be(this.lastTransaction.GetHash());
            this.caughtException.Should().BeNull();
        }

        private void ResetCaughtException()
        {
            this.caughtException = null;
        }
    }
}