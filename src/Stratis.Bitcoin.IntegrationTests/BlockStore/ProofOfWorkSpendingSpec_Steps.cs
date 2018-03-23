using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ProofOfWorkSpendingSpec
    {
        private NodeBuilder nodeBuilder;
        private CoreNode stratisBitcoinNode;
        private int coinbaseMaturity;
        private Exception caughtException;

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void proof_of_work_blocks_mined_past_maturity()
        {
            this.MineToHeight(this.coinbaseMaturity);
            this.MineToHeight(this.coinbaseMaturity);

            // the mining should add coins to the wallet
            TestHelper.WaitLoop(() => this.stratisBitcoinNode.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet")
                                          .Sum(s => s.Transaction.Amount) > Money.COIN * this.coinbaseMaturity * 50);
        }

        public static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }

        private void a_stratis_bitcoin_node_and_wallet()
        {
            this.stratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();
            this.stratisBitcoinNode.NotInIBD();

            this.stratisBitcoinNode.FullNode.WalletManager().CreateWallet("123456", "mywallet");

            this.coinbaseMaturity = (int)this.stratisBitcoinNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
        }

        private void proof_of_work_blocks_mined_to_just_before_maturity()
        {
            int targetHeight = this.coinbaseMaturity - 1;

            this.MineToHeight(targetHeight);

            // the mining should add coins to the wallet
            var total = this.stratisBitcoinNode.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet")
                .Sum(s => s.Transaction.Amount);

            Assert.Equal(Money.COIN * targetHeight * 50, total);
        }

        private void MineToHeight(int targetHeight)
        {
            var address = this.stratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));

            var wallet = this.stratisBitcoinNode.FullNode.WalletManager().GetWalletByName("mywallet");

            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress("123456", address).PrivateKey;

            this.stratisBitcoinNode.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey,
                this.stratisBitcoinNode.FullNode.Network));

            this.stratisBitcoinNode.GenerateStratisWithMiner(targetHeight);

            // wait for block repo for block sync to work
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.stratisBitcoinNode));
        }

        private void i_try_to_spend_the_coins()
        {
            // Build Transaction 
            // ====================
            // send coins to next self address
            var sendtoAddress = this.stratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference("mywallet", "account 0"), 2).ElementAt(1);

            try
            {
                this.stratisBitcoinNode.FullNode.WalletTransactionHandler().BuildTransaction(
                    CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456",
                        sendtoAddress.ScriptPubKey,
                        Money.COIN * 100, FeeType.Medium, 101));
            }
            catch (Exception exception)
            {
                this.caughtException = exception;
            }
        }

        private void the_transaction_should_be_rejected_from_the_mempool()
        {
            this.caughtException.Should().BeOfType<WalletException>();

            var walletException = (WalletException)this.caughtException;
            walletException.Message.Should().Be("No spendable transactions found.");

            // reset for future checks
            this.caughtException = null;
        }

        private void the_transaction_should_be_accepted_by_the_mempool()
        {
            this.caughtException.Should().BeNull();
        }
    }
}
