using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IMultisigTransactionHandler
    {
        /// <summary>
        /// Builds a new multisig transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <param name="secrets">List of mnemonic-passphrase pairs</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(TransactionBuildContext context, SecretModel[] secrets);
    }
}
