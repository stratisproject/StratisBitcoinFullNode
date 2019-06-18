using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;
using Recipient = Stratis.Features.FederatedPeg.Wallet.Recipient;
using TransactionBuildContext = Stratis.Features.FederatedPeg.Wallet.TransactionBuildContext;
using UnspentOutputReference = Stratis.Features.FederatedPeg.Wallet.UnspentOutputReference;

namespace Stratis.Features.FederatedPeg
{
    public interface IMultisigCoinSelector
    {
        (List<Coin>, List<UnspentOutputReference>) SelectCoins(List<Recipient> recipients);
    }

    /// <summary>
    /// Used to wrap a static method with a class that can be mocked for testing.
    /// </summary>
    public class MultisigCoinSelector : IMultisigCoinSelector
    {
        private readonly Network network;

        private readonly IFederatedPegSettings settings;
        private readonly IFederationWalletManager walletManager;

        public MultisigCoinSelector(Network network, IFederatedPegSettings settings, IFederationWalletManager walletManager)
        {
            this.network = network;
            this.settings = settings;
            this.walletManager = walletManager;
        }

        public (List<Coin>, List<UnspentOutputReference>) SelectCoins(List<Recipient> recipients)
        {
            // FederationWalletTransactionHandler only supports signing with a single key - the fed wallet key - so we don't use it to build the transaction.
            // However we still want to use it to determine what coins we need, so hack this together here to pass in to FederationWalletTransactionHandler.DetermineCoins.
            var multiSigContext = new TransactionBuildContext(recipients);

            return FederationWalletTransactionHandler.DetermineCoins(this.walletManager, this.network, multiSigContext, this.settings);
        }
    }

    /// <summary>
    /// Uses this node's federation wallet and the current network fee policy to build withdrawal transactions for the federation multisig.
    /// </summary>
    public class FedMultiSigManualWithdrawalTransactionBuilder
    {
        private readonly IFederatedPegSettings federatedPegSettings;
        
        private readonly Network network;

        private readonly IWalletFeePolicy walletFeePolicy;

        private readonly IMultisigCoinSelector multisigCoinSelector;

        public FedMultiSigManualWithdrawalTransactionBuilder(
            Network network,
            IFederatedPegSettings federatedPegSettings,
            IWalletFeePolicy walletFeePolicy,
            IMultisigCoinSelector multisigCoinSelector)
        {
            this.network = network;
            this.federatedPegSettings = federatedPegSettings;
            this.walletFeePolicy = walletFeePolicy;
            this.multisigCoinSelector = multisigCoinSelector;
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
            (List<Coin> coins, List<UnspentOutputReference> _) = this.multisigCoinSelector.SelectCoins(recipients);

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

            Money estimatedFee = transactionBuilder.EstimateFees(feeRate);

            long fee = Math.Max(estimatedFee, minTrxFee);

            var coinAmount = coins.Sum(c => c.Amount);

            var totalAmountWithFees = recipients.Sum(r => r.Amount) + fee;

            if (coinAmount < totalAmountWithFees)
            {
                // Throw an exception with a useful message.
                throw new WalletException($"Coin amount of {coinAmount} was less than total amount with fees {totalAmountWithFees}. Adjust the amount you are trying to send.");
            }
            
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