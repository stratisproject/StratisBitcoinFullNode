using System;
using System.Security;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IWalletTransactionHandler
    {
        /// <summary>
        /// Builds a new transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(TransactionBuildContext context);

        /// <summary>
        /// Adds inputs to a transaction until it has enough in value to meet its out value.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <param name="transaction">The transaction that will have more inputs added to it.</param>
        /// <remarks>
        /// This will not modify existing inputs, and will add at most one change output to the outputs.
        /// No existing outputs will be modified unless <see cref="Recipient.SubtractFeeFromAmount"/> is specified.
        /// Note that inputs which were signed may need to be resigned after completion since in/outputs have been added.
        /// The inputs added may be signed depending on whether a <see cref="TransactionBuildContext.WalletPassword"/> is passed.
        /// Note that all existing inputs must have their previous output transaction be in the wallet.
        /// </remarks>
        void FundTransaction(TransactionBuildContext context, Transaction transaction);

        /// <summary>
        /// Calculates the maximum amount a user can spend in a single transaction, taking into account the fees required.
        /// </summary>
        /// <param name="accountReference">The account from which to calculate the amount.</param>
        /// <param name="feeType">The type of fee used to calculate the maximum amount the user can spend. The higher the fee, the smaller this amount will be.</param>
        /// <param name="allowUnconfirmed"><c>true</c> to include unconfirmed transactions in the calculation, <c>false</c> otherwise.</param>
        /// <returns>The maximum amount the user can spend in a single transaction, along with the fee required.</returns>
        (Money maximumSpendableAmount, Money Fee) GetMaximumSpendableAmount(WalletAccountReference accountReference, FeeType feeType, bool allowUnconfirmed);

        /// <summary>
        /// Estimates the fee for the transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The estimated fee.</returns>
        Money EstimateFee(TransactionBuildContext context);

        /// <summary>
        /// Cache the secret for a specific wallet.
        /// If the secret is already in the cache extends its expiry according to <c>duration</c>.
        /// </summary>
        /// <param name="walletAccount">The account to cache the secret.</param>
        /// <param name="walletPassword">The password for the wallet.</param>
        /// <param name="duration">How long to cache secret for.</param>
        /// <returns>The secret being cached.</returns>
        SecureString CacheSecret(WalletAccountReference walletAccount, string walletPassword, TimeSpan duration);

        /// <summary>
        /// Clears a secret that is stored in a cache for a specific wallet.
        /// </summary>
        /// <param name="walletAccount">The account to clear the cache for the secret.</param>
        void ClearCachedSecret(WalletAccountReference walletAccount);
    }
}
