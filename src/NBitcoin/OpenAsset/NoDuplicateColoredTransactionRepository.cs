using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBitcoin.OpenAsset
{
    internal class NoDuplicateColoredTransactionRepository : IColoredTransactionRepository, ITransactionRepository
    {
        public NoDuplicateColoredTransactionRepository(IColoredTransactionRepository inner)
        {
            if(inner == null)
                throw new ArgumentNullException("inner");
            this._Inner = inner;
        }

        private IColoredTransactionRepository _Inner;
        #region IColoredTransactionRepository Members

        public ITransactionRepository Transactions
        {
            get
            {
                return this;
            }
        }

        public Task<ColoredTransaction> GetAsync(uint256 txId)
        {
            return Request("c" + txId.ToString(), () => this._Inner.GetAsync(txId));
        }

        public Task PutAsync(uint256 txId, ColoredTransaction tx)
        {
            return this._Inner.PutAsync(txId, tx);
        }

        #endregion

        #region ITransactionRepository Members

        Task<Transaction> ITransactionRepository.GetAsync(uint256 txId)
        {
            return Request("t" + txId.ToString(), () => this._Inner.Transactions.GetAsync(txId));
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return this._Inner.Transactions.PutAsync(txId, tx);
        }

        #endregion

        private Dictionary<string, Task> _Tasks = new Dictionary<string, Task>();
        private ReaderWriterLock @lock = new ReaderWriterLock();

        private Task<T> Request<T>(string key, Func<Task<T>> wrapped)
        {
            Task<T> task = null;
            using(this.@lock.LockRead())
            {
                task = this._Tasks.TryGet(key) as Task<T>;
            }
            if(task != null)
                return task;
            using(this.@lock.LockWrite())
            {
                task = this._Tasks.TryGet(key) as Task<T>;
                if(task != null)
                    return task;
                task = wrapped();
                this._Tasks.Add(key, task);
            }
            task.ContinueWith((_) =>
            {
                using(this.@lock.LockWrite())
                {
                    this._Tasks.Remove(key);
                }
            });
            return task;
        }
    }
}
