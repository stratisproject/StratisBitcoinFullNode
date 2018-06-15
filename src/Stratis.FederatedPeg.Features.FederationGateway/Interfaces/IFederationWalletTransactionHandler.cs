using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IFederationWalletTransactionHandler
    {
        /// <summary>
        /// Builds a new transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(Wallet.TransactionBuildContext context);

        /// <summary>
        /// Adds inputs to a transaction until it has enough in value to meet its out value.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <param name="transaction">The transaction that will have more inputs added to it.</param>
        /// <remarks>
        /// This will not modify existing inputs, and will add at most one change output to the outputs.
        /// No existing outputs will be modified unless <see cref="Recipient.SubtractFeeFromAmount"/> is specified.
        /// Note that inputs which were signed may need to be resigned after completion since in/outputs have been added.
        /// The inputs added may be signed depending on <see cref="TransactionBuildContext.Sign"/>, use signrawtransaction for that.
        /// Note that all existing inputs must have their previous output transaction be in the wallet.
        /// </remarks>
        void FundTransaction(Wallet.TransactionBuildContext context, Transaction transaction);

        /// <summary>
        /// Estimates the fee for the transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The estimated fee.</returns>
        Money EstimateFee(Wallet.TransactionBuildContext context);
    }
}
