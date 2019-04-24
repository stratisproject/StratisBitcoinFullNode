using System.Collections.Generic;
using System.Linq;

namespace Stratis.Features.FederatedPeg.Wallet
{
    public static class DeterministicCoinOrdering
    {
        /// <summary>
        /// Returns the unspent outputs in the preferred order of consumption.
        /// </summary>
        /// <param name="context">The context associated with the current transaction being built.</param>
        /// <returns>The unspent outputs in the preferred order of consumption.</returns>
        public static IOrderedEnumerable<UnspentOutputReference> GetOrderedUnspentOutputs(TransactionBuildContext context)
        {
            return context.UnspentOutputs.OrderBy(a => a, Comparer<UnspentOutputReference>.Create(CompareUnspentOutputReferences));
        }

        /// <summary>
        /// Compares two unspent outputs to determine the order of inclusion in the transaction.
        /// </summary>
        /// <param name="x">First unspent output.</param>
        /// <param name="y">Second unspent output.</param>
        /// <returns>Returns <c>0</c> if the outputs are the same and <c>-1<c> or <c>1</c> depending on whether the first or second output takes precedence.</returns>
        public static int CompareUnspentOutputReferences(UnspentOutputReference x, UnspentOutputReference y)
        {
            return CompareTransactionData(x.Transaction, y.Transaction);
        }

        /// <summary>
        /// Compares transaction data to determine the order of inclusion in the transaction.
        /// </summary>
        /// <param name="x">First transaction data.</param>
        /// <param name="y">Second transaction data.</param>
        /// <returns>Returns <c>0</c> if the outputs are the same and <c>-1<c> or <c>1</c> depending on whether the first or second output takes precedence.</returns>
        public static int CompareTransactionData(TransactionData x, TransactionData y)
        {
            // The oldest UTXO (determined by block height) is selected first.
            if ((x.BlockHeight ?? int.MaxValue) != (y.BlockHeight ?? int.MaxValue))
            {
                return ((x.BlockHeight ?? int.MaxValue) < (y.BlockHeight ?? int.MaxValue)) ? -1 : 1;
            }

            // If a block has more than one UTXO, then they are selected in order of transaction id.
            if (x.Id != y.Id)
            {
                return (x.Id < y.Id) ? -1 : 1;
            }

            // If multiple UTXOs appear within a transaction then they are selected in ascending index order.
            if (x.Index != y.Index)
            {
                return (x.Index < y.Index) ? -1 : 1;
            }

            return 0;
        }
    }
}
