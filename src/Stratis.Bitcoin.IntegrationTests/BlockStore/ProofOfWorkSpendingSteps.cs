using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
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
        private NodeBuilder nodeBuilder;
        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;
        private Transaction lastTransaction;
        private SharedSteps sharedSteps;

        public ProofOfWorkSpendingSpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void a_sending_and_receiving_stratis_bitcoin_node_and_wallet()
        {
            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode();
            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();
            this.sendingStratisBitcoinNode.NotInIBD();
            this.receivingStratisBitcoinNode.NotInIBD();

            this.sendingStratisBitcoinNode.CreateRPCClient().AddNode(this.receivingStratisBitcoinNode.Endpoint, true);

            this.sharedSteps.WaitForBlockStoreToSync(this.receivingStratisBitcoinNode, this.sendingStratisBitcoinNode);

            this.sendingStratisBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, SendingWalletName);
            this.receivingStratisBitcoinNode.FullNode.WalletManager().CreateWallet(WalletPassword, ReceivingWalletName);

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
                this.lastTransaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler()
                    .BuildTransaction(SharedSteps.CreateTransactionBuildContext(SendingWalletName, AccountName, WalletPassword, sendtoAddress.ScriptPubKey, Money.COIN * 1, FeeType.Medium, 101));

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