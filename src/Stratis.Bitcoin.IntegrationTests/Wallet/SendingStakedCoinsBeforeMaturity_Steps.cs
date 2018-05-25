using System.Linq;
using NBitcoin;
using FluentAssertions;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Tests.Common.TestFramework;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingStakedCoinsBeforeMaturity : BddSpecification
    {
        private ProofOfStakeSteps proofOfStakeSteps;
        private NodeGroupBuilder nodeGroupBuilder;

        private CoreNode receiverNode;

        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";
        private const string NodeReceiver = "nodereceiver";

        private const decimal OneMillion = 1_000_000;

        public SendingStakedCoinsBeforeMaturity(ITestOutputHelper outputHelper) : base(outputHelper) { }

        protected override void BeforeTest()
        {
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.CurrentTest.DisplayName);
            this.nodeGroupBuilder = new NodeGroupBuilder(this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.nodeGroupBuilder?.Dispose();
        }

        private void two_nodes_which_includes_a_proof_of_stake_wallet_with_over_a_million_coins()
        {
            this.proofOfStakeSteps.GenerateCoins();

            this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(this.proofOfStakeSteps.PosWallet)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().BeGreaterThan(Money.Coins(OneMillion));
        }

        private void a_wallet_sends_coins_before_maturity()
        {
            this.the_wallet_history_does_not_include_the_transaction();

            IActionResult sendTransactionResult = this.SendTransaction(this.BuildTransaction());

            sendTransactionResult.Should().BeOfType<ErrorResult>();

            if (!(sendTransactionResult is ErrorResult)) return;

            var error = sendTransactionResult as ErrorResult;
            error.StatusCode.Should().Be(400);

            var errorResponse = error.Value as ErrorResponse;
            errorResponse?.Errors.Count.Should().Be(1);
            errorResponse?.Errors[0].Message.Should().Be(ConsensusErrors.BadTransactionPrematureCoinbaseSpending.Message);
        }

        private IActionResult SendTransaction(IActionResult transactionResult)
        {
            var walletTransactionModel = (WalletBuildTransactionModel) (transactionResult as JsonResult)?.Value;

            if (walletTransactionModel == null) return null;

            return this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.NodeService<WalletController>()
                    .SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        private IActionResult BuildTransaction()
        {
            var transactionResult = this.proofOfStakeSteps.ProofOfStakeNodeWithCoins.FullNode.NodeService<WalletController>()
                .BuildTransaction(new BuildTransactionRequest
                {
                    AccountName = this.proofOfStakeSteps.WalletAccount,
                    AllowUnconfirmed = true,
                    Amount = Money.Coins(OneMillion + 40).ToString(),
                    DestinationAddress = this.GetReceiverUnusedAddressFromWallet(),
                    FeeType = FeeType.Medium.ToString("D"),
                    Password = this.proofOfStakeSteps.PosWalletPassword,
                    WalletName = this.proofOfStakeSteps.PosWallet,
                    FeeAmount = Money.Satoshis(20000).ToString()
                });

            return transactionResult;
        }

        private void the_wallet_history_does_not_include_the_transaction()
        {
            var walletHistory = this.GetWalletHistory(this.proofOfStakeSteps.ProofOfStakeNodeWithCoins,this.proofOfStakeSteps.PosWallet);

            var accountHistory = walletHistory.AccountsHistoryModel.FirstOrDefault();

            accountHistory?.TransactionsHistory?.Where(txn => txn.Type == TransactionItemType.Send).Count().Should().Be(0);
        }

        private void the_transaction_was_not_received()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().Be(0);
        }

        private WalletHistoryModel GetWalletHistory(CoreNode node, string walletName)
        {
            var walletHistory = node.FullNode.NodeService<WalletController>()
                .GetHistory(new WalletHistoryRequest { WalletName = walletName }) as JsonResult;

            return walletHistory?.Value as WalletHistoryModel;
        }

        private string GetReceiverUnusedAddressFromWallet()
        {
            this.receiverNode = this.CreateRecevierNode();

            this.receiverNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName);

            return this.receiverNode.FullNode.WalletManager().GetUnusedAddress(
                new WalletAccountReference(WalletName, WalletAccountName)).Address;
        }

        private CoreNode CreateRecevierNode()
        {
            return this.proofOfStakeSteps.AddAndConnectProofOfStakeNodes(NodeReceiver);
        }
    }
}
