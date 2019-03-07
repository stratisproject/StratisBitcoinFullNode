using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class WithdrawalTransactionBuilder : IWithdrawalTransactionBuilder
    {
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

                recipient = ReduceRecipientPaymentByFee(this.federationGatewaySettings.TransactionFee, recipient);

                // Build the multisig transaction template.
                uint256 opReturnData = depositId;
                string walletPassword = this.federationWalletManager.Secret.WalletPassword;
                bool sign = (walletPassword ?? "") != "";
                var multiSigContext = new TransactionBuildContext(new[] { recipient }.ToList(), opReturnData: opReturnData.ToBytes())
                {
                    OrderCoinsDeterministic = true,
                    TransactionFee = this.federationGatewaySettings.TransactionFee,
                    MinConfirmations = this.federationGatewaySettings.MinCoinMaturity,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = sign,
                };

                Transaction transaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

                // Build the transaction.
                if (this.network.Consensus.IsProofOfStake)
                {
                    transaction.Time = blockTime;

                    if (sign)
                    {
                        transaction = multiSigContext.TransactionBuilder.SignTransaction(transaction);
                    }
                }

                this.logger.LogInformation("transaction = {0}", transaction.ToString(this.network, RawFormat.BlockExplorer));

                return transaction;
            }
            catch (Exception error)
            {
                this.logger.LogError("Could not create transaction for deposit {0}: {1}", depositId, error.Message);
            }

            this.logger.LogTrace("(-)[FAIL]");
            return null;
        }

        /// <summary>
        /// We need to reduce the amount being withdrawn by the fees our transaction is going to have.
        /// </summary>
        private static Recipient ReduceRecipientPaymentByFee(Money transactionFee, Recipient recipient)
        {
            Money newAmount = recipient.Amount - transactionFee;
            return new Recipient
            {
                Amount = newAmount,
                ScriptPubKey = recipient.ScriptPubKey
            };
        }
    }
}
