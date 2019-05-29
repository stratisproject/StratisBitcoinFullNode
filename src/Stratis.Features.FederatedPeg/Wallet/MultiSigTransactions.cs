using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.Wallet
{
    public class MultiSigTransactions : ICollection<TransactionData>, ITransactionDataObserver
    {
        private readonly object lockObject;
        private Dictionary<OutPoint, TransactionData> transactionDict;
        private Dictionary<OutPoint, TransactionData> spendableTransactionDict;
        private Dictionary<uint256, List<TransactionData>> withdrawalsByDepositDict;
        private SortedDictionary<int, List<TransactionData>> spentTransactionsByHeightDict;

        public int Count => this.transactionDict.Count;

        public bool IsReadOnly => false;

        public MultiSigTransactions()
        {
            this.lockObject = new object();
            this.transactionDict = new Dictionary<OutPoint, TransactionData>();
            this.spendableTransactionDict = new Dictionary<OutPoint, TransactionData>();
            this.withdrawalsByDepositDict = new Dictionary<uint256, List<TransactionData>>();
            this.spentTransactionsByHeightDict = new SortedDictionary<int, List<TransactionData>>();
        }

        private bool TryAddWithdrawal(TransactionData transactionData)
        {
            uint256 matchingDepositId = transactionData.SpendingDetails?.WithdrawalDetails?.MatchingDepositId;

            if (matchingDepositId == null)
                return false;

            if (!this.withdrawalsByDepositDict.TryGetValue(matchingDepositId, out List<TransactionData> txList))
            {
                txList = new List<TransactionData>();
                this.withdrawalsByDepositDict.Add(transactionData.SpendingDetails.WithdrawalDetails.MatchingDepositId, txList);
            }

            txList.Add(transactionData);

            return true;
        }

        private void TryRemoveWithdrawal(TransactionData transactionData)
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
            if (!transactionData.IsSpendable() || transactionData.BlockHeight == null)
                return;

            if (!this.spentTransactionsByHeightDict.TryGetValue((int)transactionData.BlockHeight, out List<TransactionData> txList))
            {
                txList = new List<TransactionData>();
                this.spentTransactionsByHeightDict.Add((int)transactionData.BlockHeight, txList);
            }

            txList.Add(transactionData);
        }

        private void RemoveSpentTransactionByHeight(TransactionData transactionData)
        {
            if (transactionData.BlockHeight == null)
                return;

            if (this.spentTransactionsByHeightDict.TryGetValue((int)transactionData.BlockHeight, out List<TransactionData> txList))
            {
                txList.Remove(transactionData);

                if (txList.Count == 0)
                    this.spentTransactionsByHeightDict.Remove((int)transactionData.BlockHeight);
            }
        }

        public void Add(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                this.transactionDict.Add(transactionData.Key, transactionData);

                if (transactionData.IsSpendable())
                    this.spendableTransactionDict.Add(transactionData.Key, transactionData);

                this.TryAddWithdrawal(transactionData);

                this.AddSpentTransactionByHeight(transactionData);

                transactionData.Subscribe(this);
            }
        }

        public bool Remove(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                bool res = this.transactionDict.Remove(transactionData.Key);

                if (this.spendableTransactionDict.ContainsKey(transactionData.Key))
                    this.spendableTransactionDict.Remove(transactionData.Key);

                this.TryRemoveWithdrawal(transactionData);

                this.RemoveSpentTransactionByHeight(transactionData);

                transactionData.Subscribe(null);

                return res;
            }
        }

        public IEnumerator<TransactionData> GetEnumerator()
        {
            lock (this.lockObject)
            {
                return this.transactionDict.Values.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.transactionDict.Values.ToList().GetEnumerator();
        }

        public void Clear()
        {
            lock (this.lockObject)
            {
                this.transactionDict.Clear();
                this.spendableTransactionDict.Clear();
                this.withdrawalsByDepositDict.Clear();
                this.spentTransactionsByHeightDict.Clear();
            }
        }

        public void BeforeSpendingDetailsChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                if (transactionData.IsSpendable())
                    this.spendableTransactionDict.Remove(transactionData.Key);

                this.RemoveSpentTransactionByHeight(transactionData);

                this.TryRemoveWithdrawal(transactionData);
            }
        }

        public void AfterSpendingDetailsChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                if (transactionData.IsSpendable())
                    this.spendableTransactionDict.Add(transactionData.Key, transactionData);

                this.AddSpentTransactionByHeight(transactionData);

                this.TryAddWithdrawal(transactionData);
            }
        }

        public void BeforeBlockHeightChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                RemoveSpentTransactionByHeight(transactionData);
            }
        }

        public void AfterBlockHeightChanged(TransactionData transactionData)
        {
            lock (this.lockObject)
            {
                AddSpentTransactionByHeight(transactionData);
            }
        }

        public (uint256 depositId, List<TransactionData>)[] GetSpendingTransactionsByDepositId(uint256 depositId = null)
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
        /// <returns></returns>
        public TransactionData[] GetUnspentTransactions()
        {
            lock (this.lockObject)
            {
                return this.spendableTransactionDict.Select(kv => kv.Value).ToArray();
            }
        }

        public bool TryGetTransaction(uint256 transactionId, uint transactionIndex, out TransactionData transactionData)
        {
            return this.transactionDict.TryGetValue(new OutPoint(transactionId, transactionIndex), out transactionData);
        }

        public bool Contains(TransactionData item)
        {
            lock (this.lockObject)
            {
                return this.transactionDict.ContainsKey(item?.Key);
            }
        }

        public void CopyTo(TransactionData[] array, int arrayIndex)
        {
            lock (this.lockObject)
            {
                this.transactionDict.Values.CopyTo(array, arrayIndex);
            }
        }

        public (int, List<TransactionData>)[] SpentTransactionsBeforeHeight(int lessThanHeight)
        {
            lock (this.lockObject)
            {
                return this.spentTransactionsByHeightDict.TakeWhile(x => x.Key < lessThanHeight).Select(x => (x.Key, x.Value)).ToArray();
            }
        }

        public static implicit operator Dictionary<OutPoint, TransactionData>(MultiSigTransactions transactions)
        {
            return transactions.transactionDict;
        }
    }
}
