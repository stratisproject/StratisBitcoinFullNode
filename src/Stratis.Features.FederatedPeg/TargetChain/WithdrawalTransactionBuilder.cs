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
        /// <summary>
        /// The wallet should always consume UTXOs that have already been seen in a block. This makes it much easier to maintain
        /// determinism across the wallets on all the nodes.
        /// </summary>
        public const int MinConfirmations = 1;

        private readonly ILogger logger;
        private readonly Network network;

        private readonly IFederationWalletManager federationWalletManager;
        private readonly IFederationWalletTransactionBuilder federationWalletTransactionBuilder;
        private readonly IFederationGatewaySettings federationGatewaySettings;

        public WithdrawalTransactionBuilder(
            ILoggerFactory loggerFactory,
            Network network,
            IFederationWalletManager federationWalletManager,
            IFederationWalletTransactionBuilder federationWalletTransactionBuilder,
            IFederationGatewaySettings federationGatewaySettings)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.federationWalletManager = federationWalletManager;
            this.federationWalletTransactionBuilder = federationWalletTransactionBuilder;
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
                var multiSigContext = new TransactionBuildContext(new[] { recipient }.ToList(), opReturnData: opReturnData.ToBytes())
                {
                    OrderCoinsDeterministic = true,
                    TransactionFee = this.federationGatewaySettings.TransactionFee,
                    MinConfirmations = MinConfirmations,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = sign,
                    Time = this.network.Consensus.IsProofOfStake ? blockTime : (uint?) null
                };

                // Build the transaction.
                Transaction transaction = this.federationWalletTransactionBuilder.BuildTransaction(multiSigContext);

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
    }
}
