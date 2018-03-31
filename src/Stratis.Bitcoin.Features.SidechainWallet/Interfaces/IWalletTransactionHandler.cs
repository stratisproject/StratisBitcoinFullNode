using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.SidechainWallet.Interfaces
{
    /// <inheritdoc />
    public interface IWalletTransactionHandler : Wallet.Interfaces.IWalletTransactionHandler
    {
        /// <summary>
        /// Builds a new cross chain transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// These transactions target the Multisig address of the sidechain gateway, and have the end destination
        /// address in the OP_RETURN output
        /// </summary>
        /// <param name="context">The context that is used to build a new cross chain transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildCrossChainTransaction(TransactionBuildContext context);

    }
}
