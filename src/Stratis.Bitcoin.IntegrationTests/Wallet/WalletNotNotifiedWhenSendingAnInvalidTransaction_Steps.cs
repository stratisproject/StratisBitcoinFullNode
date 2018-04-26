using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using FluentAssertions;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.Utilities.Extensions;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System;
using Stratis.Bitcoin.IntegrationTests.Miners;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class WalletNotNotifiedWhenSendingAnInvalidTransaction : BddSpecification
    {
        private SharedSteps sharedSteps;
        private IDictionary<string, CoreNode> nodes;
        private NodeGroupBuilder nodeGroupBuilder;
        private TransactionBuildContext transactionBuildContext;

        private WalletAccountReference nodeWalletAccountReference;
        private HdAddress nodeAddress;
        private Features.Wallet.Wallet nodeWallet;
        private Key key;
        private Money coins;
        private WalletHistoryModel WalletHistoryBefore;
        private ProofOfStakeSteps proofOfStakeSteps;

        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private const string NodeOne = "one";
        private const string NodeTwo = "two";

        ITestOutputHelper outputHelper;

        public WalletNotNotifiedWhenSendingAnInvalidTransaction(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder?.Dispose();
        }

        private void two_nodes_which_includes_a_proof_of_stake_node_with_a_million_coins()
        {
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.nodes);
            this.proofOfStakeSteps.GenerateCoins();
        }

        private void a_wallet_with_coins()
        {
            var posPrivateKey = this.proofOfStakeSteps.GetProofOfStakeWalletKey();

            //figure out which object to use to pass into this class from proofOfStakeSteps as need
            //to get to the wallet to send money so it can fail.  `



            this.nodeAddress = this.nodes[NodeOne].FullNode.WalletManager().GetUnusedAddress(
                new WalletAccountReference(WalletName, WalletAccountName));

            this.nodeWallet = this.nodes[NodeOne].FullNode.WalletManager().GetWalletByName(WalletName);

            this.key = this.nodeWallet.GetExtendedPrivateKeyForAddress(WalletPassword, this.nodeAddress).PrivateKey;
            this.nodes[NodeOne].SetDummyMinerSecret(new BitcoinSecret(this.key, this.nodes[NodeOne].FullNode.Network));
            var maturity = (int)this.nodes[NodeOne].FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
            this.nodes[NodeOne].GenerateStratisWithMiner(1);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[NodeOne]));

            this.coins = this.nodes[NodeOne].FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName)
                .Sum(utxo => utxo.Transaction.Amount);

            this.coins.Should().Be(Money.Coins(50));
        }

        private void the_wallet_history_shows_the_transaction_as_failed_not_pending()
        {
            // get wallet history 
            var walletHistory = GetWalletHistory(this.nodes[NodeOne]);

            // check to see if the failed transaction is there.
        }

        private void a_wallet_sends_all_coins_and_fails()
        {            
            this.WalletHistoryBefore = GetWalletHistory(this.nodes[NodeOne]);
            this.WalletHistoryBefore?.AccountsHistoryModel.Count.Should().Be(1);

            // send all coins
            var sendToAddress = this.nodes[NodeTwo].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));

            var powMinting = new ProofOfStakeMintCoinsSpecification(this.outputHelper);

            //powMinting.ReceiveOneMillionCoins(sendToAddress);
            powMinting.Should().Be(Money.Coins(1000000));



            // check mem pool to make sure transaction failed

        }

        private void a_wallet_sends_coins_with_a_high_fee_type()
        {
        }

        private WalletHistoryModel GetWalletHistory(CoreNode node)
        {
            var restWallet = this.nodes[NodeOne].FullNode.NodeService<WalletController>();

            var result = restWallet.GetHistory(new WalletHistoryRequest { WalletName = WalletName });
            var viewResult = result as JsonResult;
            var model = viewResult.Value as WalletHistoryModel;

            return model;

        }
    }
}
