using System.Linq;
using NBitcoin;
using FluentAssertions;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingRecentlyStakedCoinsBeforeMaturity : BddSpecification
    {
        private SharedSteps sharedSteps;
        private ProofOfStakeSteps proofOfStakeSteps;
        private NodeGroupBuilder nodeGroupBuilder;

        private CoreNode SenderNode;
        private CoreNode ReceiverNode;

        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private const string NodeReceiver = "nodereceiver";

        ITestOutputHelper outputHelper;

        public SendingRecentlyStakedCoinsBeforeMaturity(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.proofOfStakeSteps = new ProofOfStakeSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder?.Dispose();
        }

        private void two_nodes_which_includes_a_proof_of_stake_node_with_a_million_coins()
        {
            this.proofOfStakeSteps.GenerateCoins();

            this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(this.proofOfStakeSteps.PosWallet)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(Money.Coins(1000000));
        }

        private void a_wallet_sends_staked_coins_before_maturity()
        {
            this.SenderNode = this.proofOfStakeSteps.ProofOfStakeNodeWithCoins;

            GetWalletHistory(this.SenderNode, this.proofOfStakeSteps.PosWallet)
                .AccountsHistoryModel
                .FirstOrDefault()
                .TransactionsHistory
                .Where(txn => txn.Type == TransactionItemType.Send).Count().Should().Be(0);

            var transactionResult = this.SenderNode.FullNode.NodeService<WalletController>().BuildTransaction(new BuildTransactionRequest
            {
                AccountName = this.proofOfStakeSteps.WalletAccount,
                AllowUnconfirmed = true,
                Amount = new Money(1000000).ToString(),
                DestinationAddress = GetReceiverUnusedAddressFromWallet(), 
                FeeType = FeeType.Medium.ToString("D"),
                Password = this.proofOfStakeSteps.PosWalletPassword,
                WalletName = this.proofOfStakeSteps.PosWallet,
                FeeAmount = Money.Satoshis(20000).ToString()
            });

            var walletTransactionModel = (transactionResult as JsonResult).Value as WalletBuildTransactionModel;

            this.SenderNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        private void the_wallet_history_shows_the_transaction_as_sent()
        {
            GetWalletHistory(this.SenderNode, this.proofOfStakeSteps.PosWallet)
                .AccountsHistoryModel
                .FirstOrDefault()
                .TransactionsHistory
                .Where(txn => txn.Type == TransactionItemType.Send).Count().Should().Be(1);

            this.ReceiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(0);
        }

        private WalletHistoryModel GetWalletHistory(CoreNode node, string walletName)
        {
            var walletHistory = node.FullNode.NodeService<WalletController>()
                .GetHistory(new WalletHistoryRequest { WalletName = walletName }) as JsonResult;

            return walletHistory.Value as WalletHistoryModel;
        }

        private string GetReceiverUnusedAddressFromWallet()
        {
            this.ReceiverNode = CreateRecevierNode();

            this.ReceiverNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName);

            return this.ReceiverNode.FullNode.WalletManager().GetUnusedAddress(
                new WalletAccountReference(WalletName, WalletAccountName)).Address;
        }

        private CoreNode CreateRecevierNode()
        {
            return this.proofOfStakeSteps.AddAndConnectProofOfStakeNodes(NodeReceiver);
        }
    }
}
