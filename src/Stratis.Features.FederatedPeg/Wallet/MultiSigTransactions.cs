using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Maintains and observes a collection of <see cref="TransactionData"/> objects.
    /// Automatically updates the lookups when certain fields of the child objects change.
    /// </summary>
    public class MultiSigTransactions : LockProtected, ICollection<TransactionData>, ITransactionDataObserver
    {
        private readonly Dictionary<OutPoint, TransactionData> transactionDict;
        private readonly SortedList<TransactionData, TransactionData> spendableTransactionList;
        private readonly Dictionary<uint256, List<TransactionData>> withdrawalsByDepositDict;
        private readonly SortedDictionary<int, List<TransactionData>> spentTransactionsByHeightDict;

        public int Count => this.transactionDict.Count;

        public bool IsReadOnly => false;

        public MultiSigTransactions() : base()
        {
            this.transactionDict = new Dictionary<OutPoint, TransactionData>();
            this.spendableTransactionList = new SortedList<TransactionData, TransactionData>(Comparer<TransactionData>.Create(DeterministicCoinOrdering.CompareTransactionData));
            this.withdrawalsByDepositDict = new Dictionary<uint256, List<TransactionData>>();
            this.spentTransactionsByHeightDict = new SortedDictionary<int, List<TransactionData>>();
        }

        private void AddWithdrawal(TransactionData transactionData)
        {
            uint256 matchingDepositId = transactionData.SpendingDetails?.WithdrawalDetails?.MatchingDepositId;

            if (matchingDepositId == null)
                return;

            if (!this.withdrawalsByDepositDict.TryGetValue(matchingDepositId, out List<TransactionData> txList))
            {
                txList = new List<TransactionData>();
                this.withdrawalsByDepositDict.Add(transactionData.SpendingDetails.WithdrawalDetails.MatchingDepositId, txList);
            }

            txList.Add(transactionData);
        }

        private void RemoveWithdrawal(TransactionData transactionData)
        {
            uint256 matchingDepositId = transactionData.SpendingDetails?.WithdrawalDetails?.MatchingDepositId;

            if (matchingDepositId == null)
                return;

            if (this.withdrawalsByDepositDict.TryGetValue(matchingDepositId, out List<TransactionData> txList))
            {
                txList.Remove(transactionData);

                if (txList.Count == 0)
                    this.withdrawalsByDepositDict.Remove(matchingDepositId);
            }
        }

        private void AddSpentTransactionByHeight(TransactionData transactionData)
        {
            if (transactionData.IsSpendable() || transactionData.SpendingDetails.BlockHeight == null)
                return;

            if (!this.spentTransactionsByHeightDict.TryGetValue((int)transactionData.SpendingDetails.BlockHeight, out List<TransactionData> txList))
            {
                txList = new List<TransactionData>();
                this.spentTransactionsByHeightDict.Add((int)transactionData.SpendingDetails.BlockHeight, txList);
            }

            txList.Add(transactionData);
        }

        private void RemoveSpentTransactionByHeight(TransactionData transactionData)
        {
            if (transactionData.SpendingDetails?.BlockHeight == null)
                return;

            if (this.spentTransactionsByHeightDict.TryGetValue((int)transactionData.SpendingDetails.BlockHeight, out List<TransactionData> txList))
            {
                txList.Remove(transactionData);

                if (txList.Count == 0)
                    this.spentTransactionsByHeightDict.Remove((int)transactionData.SpendingDetails.BlockHeight);
            }
        }

        /// <summary>
        /// Adds a <see cref="TransactionData"/> object to the collection and updates the lookups.
        /// </summary>
        /// <param name="transactionData">The <see cref="TransactionData"/> object to add to the collection</param>.
        [NoTrace]
        public void Add(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                this.transactionDict.Add(transactionData.Key, transactionData);

                if (transactionData.IsSpendable())
                    this.spendableTransactionList.Add(transactionData, transactionData);

                this.AddWithdrawal(transactionData);

                this.AddSpentTransactionByHeight(transactionData);

                transactionData.Subscribe(this);
            }
        }

        /// <summary>
        /// Removes a <see cref="TransactionData"/> object from the collection and updates the lookups.
        /// </summary>
        /// <param name="transactionData">The <see cref="TransactionData"/> object to remove from the collection.</param>
        /// <returns><c>True</c> if the object existed and <c>false</c> otherwise.</returns>
        [NoTrace]
        public bool Remove(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                bool res = this.transactionDict.Remove(transactionData.Key);

                if (this.spendableTransactionList.ContainsKey(transactionData))
                    this.spendableTransactionList.Remove(transactionData);

                this.RemoveWithdrawal(transactionData);

                this.RemoveSpentTransactionByHeight(transactionData);

                transactionData.Subscribe(null);

                return res;
            }
        }

        /// <summary>
        /// Gets an enumerator for the collection.
        /// </summary>
        /// <returns>Returns an enumerator for the collection.</returns>
        [NoTrace]
        public IEnumerator<TransactionData> GetEnumerator()
        {
            lock (this.lockObject)
            {
                return this.transactionDict.Values.ToList().GetEnumerator();
            }
        }

        /// <summary>
        /// Gets an enumerator for the collection.
        /// </summary>
        /// <returns>Returns an enumerator for the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.transactionDict.Values.ToList().GetEnumerator();
        }

        /// <summary>
        /// Clears the collection and lookups.
        /// </summary>
        [NoTrace]
        public void Clear()
        {
            lock (this.lockObject)
            {
                this.transactionDict.Clear();
                this.spendableTransactionList.Clear();
                this.withdrawalsByDepositDict.Clear();
                this.spentTransactionsByHeightDict.Clear();
            }
        }

        /// <summary>
        /// Called by the <see cref="TransactionData"/> child object to let us know
        /// that its <see cref="TransactionData.SpendingDetails"/> is about to change.
        /// </summary>
        /// <param name="transactionData">The <see cref="TransactionData"/> object notifying us.</param>
        [NoTrace]
        public void BeforeSpendingDetailsChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                if (this.spendableTransactionList.ContainsKey(transactionData))
                    this.spendableTransactionList.Remove(transactionData);

                this.RemoveSpentTransactionByHeight(transactionData);

                this.RemoveWithdrawal(transactionData);
            }
        }

        /// <summary>
        /// Called by the <see cref="TransactionData"/> child object to let us know
        /// that its <see cref="TransactionData.SpendingDetails"/> has changed.
        /// </summary>
        /// <param name="transactionData">The <see cref="TransactionData"/> object notifying us.</param>
        [NoTrace]
        public void AfterSpendingDetailsChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                if (transactionData.IsSpendable())
                    this.spendableTransactionList.Add(transactionData, transactionData);

                this.AddSpentTransactionByHeight(transactionData);

                this.AddWithdrawal(transactionData);
            }
        }

        /// <summary>
        /// Called by the <see cref="TransactionData"/> child object to let us know
        /// that its <see cref="TransactionData.BlockHeight"/> is about to change.
        /// </summary>
        /// <param name="transactionData">The <see cref="TransactionData"/> object notifying us.</param>
        [NoTrace]
        public void BeforeBlockHeightChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                this.RemoveSpentTransactionByHeight(transactionData);

                // This is ordered by height/id/index.
                if (this.spendableTransactionList.ContainsKey(transactionData))
                    this.spendableTransactionList.Remove(transactionData);
            }
        }

        /// <summary>
        /// Called by the <see cref="TransactionData"/> child object to let us know
        /// that its <see cref="TransactionData.BlockHeight"/> has changed.
        /// </summary>
        /// <param name="transactionData">The <see cref="TransactionData"/> object notifying us.</param>
        [NoTrace]
        public void AfterBlockHeightChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                this.AddSpentTransactionByHeight(transactionData);

                // This is ordered by height/id/index.
                if (transactionData.IsSpendable())
                    this.spendableTransactionList.Add(transactionData, transactionData);
            }
        }

        /// <summary>
        /// Gets the <see cref="TransactionData"/> objects related to a specific deposit or all if none is specified.
        /// </summary>
        /// <param name="depositId">The deposit id of the deposit.</param>
        /// <returns>The <see cref="TransactionData"/> objects related to the provided deposit id. Returns all if not specified.</returns>
        [NoTrace]
        public (uint256 depositId, List<TransactionData> txList)[] GetSpendingTransactionsByDepositId(uint256 depositId = null)
        {
            lock (this.lockObject)
            {
                if (depositId != null)
                {
                    if (this.withdrawalsByDepositDict.TryGetValue(depositId, out List<TransactionData> txList))
                        return new[] { (depositId, txList) };
                    else
                        return new[] { (depositId, new List<TransactionData>()) };
                }

                return this.withdrawalsByDepositDict.Select(kv => (kv.Key, kv.Value)).ToArray();
            }
        }

        /// <summary>
        /// List all spendable transactions in a multisig address.
        /// </summary>
        /// <returns>Returns all the unspent <see cref="TransactionData"/> objects.</returns>
        [NoTrace]
        public TransactionData[] GetUnspentTransactions()
        {
            lock (this.lockObject)
            {
                return this.spendableTransactionList.Keys.ToArray();
            }
        }

        /// <summary>
        /// Attempts to retrieve a <see cref="TransactionData"/> object identified by transaction id and index.
        /// </summary>
        /// <param name="transactionId">The transaction id of the object to retrieve.</param>
        /// <param name="transactionIndex">The transaction index og the object to retrieve.</param>
        /// <param name="transactionData">The retrieved object if any.</param>
        /// <returns><c>True</c> if the object was found and <c>false</c> otherwise.</returns>
        [NoTrace]
        public bool TryGetTransaction(uint256 transactionId, int transactionIndex, out TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                return this.transactionDict.TryGetValue(new OutPoint(transactionId, transactionIndex), out transactionData);
            }
        }

        /// <summary>
        /// Determines if the collection contains a particular <see cref="TransactionData"/> object.
        /// </summary>
        /// <param name="item">The  <see cref="TransactionData"/> object to find in the collection.</param>
        /// <returns><c>True</c> if the object was found and <c>false</c> otherwise.</returns>
        [NoTrace]
        public bool Contains(TransactionData item)
        {
            lock (this.lockObject)
            {
                return this.transactionDict.ContainsKey(item?.Key);
            }
        }

        /// <summary>
        /// Copies the contents of the collection to an array at a specified index.
        /// </summary>
        /// <param name="array">The array to copy the collection to.</param>
        /// <param name="arrayIndex">The array position to copy the collection to.</param>
        [NoTrace]
        public void CopyTo(TransactionData[] array, int arrayIndex)
        {
            lock (this.lockObject)
            {
                this.transactionDict.Values.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Finds all <see cref="TransactionData" objects that have block heights below the specified value.
        /// </summary>
        /// <param name="lessThanHeight">The block height to find objects below.</param>
        /// <returns>The <see cref="TransactionData" objects that have block heights below the specified value.</returns>
        [NoTrace]
        public (int, List<TransactionData>)[] SpentTransactionsBeforeHeight(int lessThanHeight)
        {
            lock (this.lockObject)
            {
                return this.spentTransactionsByHeightDict.TakeWhile(x => x.Key < lessThanHeight).Select(x => (x.Key, x.Value)).ToArray();
            }
        }

        /// <summary>
        /// Provides dictionary access to the collection of <see cref="TransactionData"/> objects keyed on <see cref="OutPoint"/>.
        /// </summary>
        /// <returns>A dictionary of <see cref="TransactionData"/> objects keyed on <see cref="OutPoint"/>.</returns>
        [NoTrace]
        public Dictionary<OutPoint, TransactionData> GetOutpointLookup()
        {
            return this.transactionDict;
        }
    }
}
