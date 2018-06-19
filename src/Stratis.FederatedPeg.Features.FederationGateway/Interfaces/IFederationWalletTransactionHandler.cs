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
    }
}
