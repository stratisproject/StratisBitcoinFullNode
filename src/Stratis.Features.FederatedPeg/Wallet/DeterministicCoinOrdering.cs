using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.Wallet
{
    [NoTrace]
    public static class DeterministicCoinOrdering
    {
        /// <summary>
        /// Returns the unspent outputs in the preferred order of consumption.
        /// </summary>
        /// <param name="unspentOutputs">The unspent outputs to order.</param>
        /// <returns>The unspent outputs in the preferred order of consumption.</returns>
        public static IOrderedEnumerable<UnspentOutputReference> GetOrderedUnspentOutputs(List<UnspentOutputReference> unspentOutputs)
        {
            return unspentOutputs.OrderBy(a => a, Comparer<UnspentOutputReference>.Create(CompareUnspentOutputReferences));
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

        public static int CompareUint256(uint256 x, uint256 y)
        {
            if (x == y)
                return 0;

            return (x < y) ? -1 : 1;
        }
    }
}
