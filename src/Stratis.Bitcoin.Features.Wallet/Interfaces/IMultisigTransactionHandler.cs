using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IMultisigTransactionHandler
    {
        /// <summary>
        /// Builds a new transaction based on information from the <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">The context that is used to build a new transaction.</param>
        /// <returns>The new transaction.</returns>
        Transaction BuildTransaction(TransactionBuildContext context, string[] mnemonics);
    }
}
