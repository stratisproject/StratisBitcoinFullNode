using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;
using TransactionBuildContext = Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class WithdrawalTransactionBuilder : IWithdrawalTransactionBuilder
    {
        /// <summary>
        /// The wallet should always consume UTXOs that have already been seen in a block. This makes it much easier to maintain
        /// determinism across the wallets on all the nodes.
        /// </summary>
        public const int MinConfirmations = 1;

        private readonly ILogger logger;
        private readonly Network network;

        private readonly IFederationWalletManager federationWalletManager;
        private readonly IFederationWalletTransactionHandler federationWalletTransactionHandler;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        public WithdrawalTransactionBuilder(
            ILoggerFactory loggerFactory,
            Network network,
            IFederationWalletManager federationWalletManager,
            IFederationWalletTransactionHandler federationWalletTransactionHandler,
            IFederationGatewaySettings federationGatewaySettings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.federationWalletManager = federationWalletManager;
            this.federationWalletTransactionHandler = federationWalletTransactionHandler;
            this.federationGatewaySettings = federationGatewaySettings;
        }

        /// <inheritdoc />
        public Transaction BuildWithdrawalTransaction(uint256 depositId, uint blockTime, Recipient recipient)
        {
            try
            {
                this.logger.LogInformation("BuildDeterministicTransaction depositId(opReturnData)={0} recipient.ScriptPubKey={1} recipient.Amount={2}", depositId, recipient.ScriptPubKey, recipient.Amount);

                // Build the multisig transaction template.
                uint256 opReturnData = depositId;
                string walletPassword = this.federationWalletManager.Secret.WalletPassword;
                bool sign = (walletPassword ?? "") != "";
                var multiSigContext = new TransactionBuildContext(new[]
                {
                    recipient.WithPaymentReducedByFee(this.federationGatewaySettings.TransactionFee)
                }.ToList(), opReturnData: opReturnData.ToBytes())
                {
                    TransactionFee = this.federationGatewaySettings.TransactionFee,
                    MinConfirmations = MinConfirmations,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = sign,
                    Time = this.network.Consensus.IsProofOfStake ? blockTime : (uint?) null
                };

                // Build the transaction.
                Transaction transaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

                this.logger.LogInformation("transaction = {0}", transaction.ToString(this.network, RawFormat.BlockExplorer));

                return transaction;
            }
            catch (Exception error)
            {
                if (error is WalletException walletException && 
                    (walletException.Message == FederationWalletTransactionHandler.NoSpendableTransactionsMessage
                     || walletException.Message == FederationWalletTransactionHandler.NotEnoughFundsMessage))
                {
                    this.logger.LogWarning("Not enough spendable transactions in the wallet. Should be resolved when a pending transaction is included in a block.");
                }
                else
                {
                    this.logger.LogError("Could not create transaction for deposit {0}: {1}", depositId, error.Message);
                }
            }

            this.logger.LogTrace("(-)[FAIL]");
            return null;
        }
    }
}
