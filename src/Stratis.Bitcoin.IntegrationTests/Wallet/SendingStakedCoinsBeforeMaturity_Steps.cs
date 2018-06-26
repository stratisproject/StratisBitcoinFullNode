using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public partial class SendingStakedCoinsBeforeMaturity : BddSpecification
    {
        private ProofOfStakeSteps proofOfStakeSteps;
        private const string NodeReceiver = "nodereceiver";
        private const decimal OneMillion = 1_000_000;
        private CoreNode receiverNode;
        private const string WalletName = "mywallet";
        private const string WalletPassword = "123456";
        private const string WalletAccountName = "account 0";

        public SendingStakedCoinsBeforeMaturity(ITestOutputHelper outputHelper)
            : base(outputHelper)
        { }

        protected override void BeforeTest()
        {
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.CurrentTest.DisplayName);
        }

        protected override void AfterTest()
        {
            this.proofOfStakeSteps.NodeGroupBuilder?.Dispose();
        }

        private void two_pos_nodes_with_one_node_having_a_wallet_with_premined_coins()
        {
            this.proofOfStakeSteps.GenerateCoins();

            this.proofOfStakeSteps.PremineNodeWithCoins.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(this.proofOfStakeSteps.PremineWallet)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().BeGreaterThan(Money.Coins(OneMillion));

            this.receiverNode = this.proofOfStakeSteps.NodeGroupBuilder
                                    .CreateStratisPosNode(NodeReceiver)
                                    .Start()
                                    .NotInIBD()
                                    .WithWallet(WalletName, WalletPassword)
                                    .Build()[NodeReceiver];
        }

        private void a_wallet_sends_coins_before_maturity()
        {
            this.the_wallet_history_does_not_include_the_transaction();

            IActionResult sendTransactionResult = this.SendTransaction(this.BuildTransaction());

            sendTransactionResult.Should().BeOfType<ErrorResult>();

            if (!(sendTransactionResult is ErrorResult))
                return;

            var error = sendTransactionResult as ErrorResult;
            error.StatusCode.Should().Be(400);

            var errorResponse = error.Value as ErrorResponse;
            errorResponse?.Errors.Count.Should().Be(1);
            errorResponse?.Errors[0].Message.Should().Be(ConsensusErrors.BadTransactionPrematureCoinstakeSpending.Message);
        }

        private IActionResult SendTransaction(IActionResult transactionResult)
        {
            var walletTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;
            if (walletTransactionModel == null)
                return null;

            return this.proofOfStakeSteps.PremineNodeWithCoins.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        private IActionResult BuildTransaction()
        {
            IActionResult transactionResult = this.proofOfStakeSteps.PremineNodeWithCoins.FullNode.NodeService<WalletController>()
                .BuildTransaction(new BuildTransactionRequest
                {
                    AccountName = this.proofOfStakeSteps.PremineWalletAccount,
                    AllowUnconfirmed = true,
                    Amount = Money.Coins(OneMillion + 40).ToString(),
                    DestinationAddress = this.GetReceiverUnusedAddressFromWallet(),
                    FeeType = FeeType.Medium.ToString("D"),
                    Password = this.proofOfStakeSteps.PremineWalletPassword,
                    WalletName = this.proofOfStakeSteps.PremineWallet,
                    FeeAmount = Money.Satoshis(20000).ToString()
                });

            return transactionResult;
        }

        private void the_wallet_history_does_not_include_the_transaction()
        {
            WalletHistoryModel walletHistory = this.GetWalletHistory(this.proofOfStakeSteps.PremineNodeWithCoins, this.proofOfStakeSteps.PremineWallet);
            AccountHistoryModel accountHistory = walletHistory.AccountsHistoryModel.FirstOrDefault();

            accountHistory?.TransactionsHistory?.Where(txn => txn.Type == TransactionItemType.Send).Count().Should().Be(0);
        }

        private void the_transaction_was_not_received()
        {
            this.receiverNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(utxo => utxo.Transaction.Amount).Should().Be(0);
        }

        private WalletHistoryModel GetWalletHistory(CoreNode node, string walletName)
        {
            var walletHistory = node.FullNode.NodeService<WalletController>().GetHistory(new WalletHistoryRequest { WalletName = walletName }) as JsonResult;
            return walletHistory?.Value as WalletHistoryModel;
        }

        private string GetReceiverUnusedAddressFromWallet()
        {
            this.proofOfStakeSteps.NodeGroupBuilder.WithConnections().Connect(this.proofOfStakeSteps.PremineNode, NodeReceiver);
            return this.receiverNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName)).Address;
        }
    }
}