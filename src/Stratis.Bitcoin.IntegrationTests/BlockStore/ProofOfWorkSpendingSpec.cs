using System;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;
using Xunit.Sdk;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public class ProofOfWorkSpendingSpec : BddSpecification
    {
        private NodeBuilder nodeBuilder;

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        [Fact]
        public void Attempt_to_spend_coin_earned_through_proof_of_work_as_soon_as_it_is_mined_will_fail()
        {
            //Given(a_stratis_bitcoin_node_on_regtest);
            //And(proof_of_work_blocks_have_just_been_mined);
            //And(maturity_has_not_been_reached);
            //When(i_try_to_spend_the_coins);
            //Then(then_transaction_should_be_rejected_from_the_mempool);

            var stratisSender = this.nodeBuilder.CreateStratisPowNode();

            this.nodeBuilder.StartAll();
            stratisSender.NotInIBD();

            // get a key from the wallet
            var mnemonic = stratisSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");

            var address = stratisSender.FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));

            var wallet = stratisSender.FullNode.WalletManager().GetWalletByName("mywallet");
            var extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress("123456", address).PrivateKey;

            stratisSender.SetDummyMinerSecret(new BitcoinSecret(extendedPrivateKey, stratisSender.FullNode.Network));

            var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            int targetHeight = maturity - 1;

            stratisSender.GenerateStratisWithMiner(targetHeight);

            // wait for block repo for block sync to work
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));

            // the mining should add coins to the wallet
            var total = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet")
                .Sum(s => s.Transaction.Amount);
            Assert.Equal(Money.COIN * targetHeight * 50, total);

            // Build Transaction 
            // ====================
            // send coins to next self address
            var sendtoAddress = stratisSender.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference("mywallet", "account 0"), 2).ElementAt(1);

            Action buildATransactionWithImmatureCoins = () =>
            {
                stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(
                    CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456",
                        sendtoAddress.ScriptPubKey,
                        Money.COIN * 100, FeeType.Medium, 101));
            };

            buildATransactionWithImmatureCoins
                .Should().Throw<WalletException>().WithMessage("No spendable transactions found.");


            //// wait for the trx to arrive
            //TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
            //Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), false));
            //TestHelper.WaitLoop(() =>
            //    stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

            //var receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet")
            //    .Sum(s => s.Transaction.Amount);
            //Assert.Equal(Money.COIN * 100, receivetotal);
            //Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First()
            //    .Transaction.BlockHeight);

            //// generate two new blocks so the trx is confirmed
            //stratisSender.GenerateStratisWithMiner(1);
            //var transaction1MinedHeight = targetHeight + 1;
            //stratisSender.GenerateStratisWithMiner(1);
            //targetHeight = targetHeight + 2;

            //// wait for block repo for block sync to work
            //TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
            //TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
            //TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
            //Assert.Equal(targetHeight, stratisReceiver.FullNode.Chain.Tip.Height);
            //TestHelper.WaitLoop(() => transaction1MinedHeight == stratisReceiver.FullNode.WalletManager()
            //                              .GetSpendableTransactionsInWallet("mywallet").First().Transaction
            //                              .BlockHeight);
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

        private void a_stratis_bitcoin_node_on_regtest()
        {
            throw new System.NotImplementedException();
        }

        private void proof_of_work_blocks_have_just_been_mined()
        {
            throw new System.NotImplementedException();
        }

        private void maturity_has_not_been_reached()
        {
            throw new System.NotImplementedException();
        }

        private void i_try_to_spend_the_coins()
        {
            throw new System.NotImplementedException();
        }

        private void then_transaction_should_be_rejected_from_the_mempool()
        {
            throw new System.NotImplementedException();
        }
    }
}
