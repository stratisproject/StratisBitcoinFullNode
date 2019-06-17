using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>
    /// Uses this node's federation wallet and the current network fee policy to build withdrawal transactions for the federation multisig.
    /// </summary>
    public class FedMultiSigWithdrawalTransactionBuilder
    {
        private readonly IFederationWalletManager federationWalletManager;

        private readonly IFederatedPegSettings federatedPegSettings;
        
        private readonly Network network;

        private readonly IWalletFeePolicy walletFeePolicy;

        public FedMultiSigWithdrawalTransactionBuilder(
            Network network,
            IFederationWalletManager federationWalletManager,
            IFederatedPegSettings federatedPegSettings,
            IWalletFeePolicy walletFeePolicy)
        {
            this.network = network;
            this.federationWalletManager = federationWalletManager;
            this.federatedPegSettings = federatedPegSettings;
            this.walletFeePolicy = walletFeePolicy;
        }

        /// <summary>
        /// Builds a withdrawal transaction from the federation multisig to the given recipients.
        /// Determines which coins from the federation wallet to spend for the total amount being sent to recipients.
        /// Calculates the fee based on the wallet fee policy.
        /// Sets the change address to be the multisig redeem script hash.
        /// Signs the transaction using the given private keys.
        /// </summary>
        /// <param name="recipients">The recipients of the transaction.</param>
        /// <param name="privateKeys">The private keys of the multisig.</param>
        /// <returns></returns>
        public Transaction BuildTransaction(List<Recipient> recipients, Key[] privateKeys)
        {
            // FederationWalletTransactionHandler only supports signing with a single key - the fed wallet key - so we don't use it to build the transaction.
            // However we still want to use it to determine what coins we need, so hack this together here to pass in to FederationWalletTransactionHandler.DetermineCoins.
            var multiSigContext = new Wallet.TransactionBuildContext(recipients);

            (List<Coin> coins, List<Wallet.UnspentOutputReference> _) = FederationWalletTransactionHandler.DetermineCoins(this.federationWalletManager, this.network, multiSigContext, this.federatedPegSettings);

            // MultiSigAddress from the wallet is not safe. It's only pulled from multisig-wallet.json and can
            // be different from the *actual* multisig address for the current redeem script.
            // Instead, use the address from settings - it's derived from the redeem script provided at startup.
            var multiSigAddress = this.federatedPegSettings.MultiSigAddress.ScriptPubKey;

            var transactionBuilder = new TransactionBuilder(this.network);

            transactionBuilder.AddCoins(coins);
            transactionBuilder.SetChange(multiSigAddress);
            transactionBuilder.AddKeys(privateKeys);
            transactionBuilder.SetTimeStamp(Utils.DateTimeToUnixTime(DateTimeOffset.UtcNow));

            var minTrxFee = new Money(this.network.MinTxFee, MoneyUnit.Satoshi);

            FeeRate feeRate = this.walletFeePolicy.GetFeeRate(FeeType.Medium.ToConfirmations());

            long fee = Math.Max(transactionBuilder.EstimateFees(feeRate), minTrxFee);

            transactionBuilder.SendFees(fee);

            foreach (Recipient recipient in recipients)
            {
                transactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
            }

            Transaction transaction = transactionBuilder.BuildTransaction(true);

            return transaction;
        }
    }
}