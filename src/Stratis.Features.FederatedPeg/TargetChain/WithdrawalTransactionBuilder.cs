using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;
using Stratis.SmartContracts.Core.State;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;
using TransactionBuildContext = Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class BuildWithdrawalTransactionResult
    {
        /// <summary>Set if the transaction was successfully built.</summary>
        public bool Success => this.Transaction != null;

        /// <summary>Set if the transaction can't be retried - e.g. not simply a balance issue.</summary>
        public bool Reject { get; set; }

        /// <summary>The transaction that was built or <c>null</c> otherwise.</summary>
        public Transaction Transaction { get; set; }
    }

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
        private readonly IFederatedPegSettings federatedPegSettings;
        private readonly IStateRepositoryRoot stateRepositoryRoot;

        public WithdrawalTransactionBuilder(
            ILoggerFactory loggerFactory,
            Network network,
            IFederationWalletManager federationWalletManager,
            IFederationWalletTransactionHandler federationWalletTransactionHandler,
            IFederatedPegSettings federatedPegSettings,
            IStateRepositoryRoot stateRepositoryRoot = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.federationWalletManager = federationWalletManager;
            this.federationWalletTransactionHandler = federationWalletTransactionHandler;
            this.federatedPegSettings = federatedPegSettings;
            this.stateRepositoryRoot = stateRepositoryRoot;
        }

        /// <inheritdoc />
        public BuildWithdrawalTransactionResult BuildWithdrawalTransaction(uint256 depositId, uint blockTime, Recipient recipient)
        {
            try
            {
                this.logger.LogDebug("BuildDeterministicTransaction depositId(opReturnData)={0} recipient.ScriptPubKey={1} recipient.Amount={2}", depositId, recipient.ScriptPubKey, recipient.Amount);

                // Can't send funds to known contract address.
                if (this.stateRepositoryRoot != null)
                {
                    KeyId p2pkhParams = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(recipient.ScriptPubKey);

                    if (p2pkhParams != null && this.stateRepositoryRoot.GetAccountState(new uint160(p2pkhParams.ToBytes())) != null)
                    {
                        return new BuildWithdrawalTransactionResult() { Reject = true };
                    }
                }

                // Build the multisig transaction template.
                uint256 opReturnData = depositId;
                string walletPassword = this.federationWalletManager.Secret.WalletPassword;
                bool sign = (walletPassword ?? "") != "";

                var multiSigContext = new TransactionBuildContext(new List<Recipient>(), opReturnData: opReturnData.ToBytes())
                {
                    MinConfirmations = MinConfirmations,
                    Shuffle = false,
                    IgnoreVerify = true,
                    WalletPassword = walletPassword,
                    Sign = sign,
                    Time = this.network.Consensus.IsProofOfStake ? blockTime : (uint?) null
                };

                multiSigContext.Recipients = new List<Recipient> { recipient.WithPaymentReducedByFee(FederatedPegSettings.CrossChainTransferFee) }; // The fee known to the user is taken.

                // TODO: Amend this so we're not picking coins twice.
                (List<Coin> coins, List<Wallet.UnspentOutputReference> unspentOutputs) = FederationWalletTransactionHandler.DetermineCoins(this.federationWalletManager, this.network, multiSigContext, this.federatedPegSettings);

                multiSigContext.TransactionFee = this.federatedPegSettings.GetWithdrawalTransactionFee(coins.Count); // The "actual fee". Everything else goes to the fed.
                multiSigContext.SelectedInputs = unspentOutputs.Select(u => u.ToOutPoint()).ToList();
                multiSigContext.AllowOtherInputs = false;

                // Build the transaction.
                Transaction transaction = this.federationWalletTransactionHandler.BuildTransaction(multiSigContext);

                this.logger.LogDebug("transaction = {0}", transaction.ToString(this.network, RawFormat.BlockExplorer));

                return new BuildWithdrawalTransactionResult() { Transaction = transaction };
            }
            catch (Exception error)
            {
                var res = new BuildWithdrawalTransactionResult();

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

                this.logger.LogTrace("(-)[FAIL]");
                return res;
            }
        }
    }
}
