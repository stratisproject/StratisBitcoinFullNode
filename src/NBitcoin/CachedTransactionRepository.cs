using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBitcoin
{
    public class CachedTransactionRepository : ITransactionRepository
    {
        private ITransactionRepository _Inner;
        private Dictionary<uint256, Transaction> _Transactions = new Dictionary<uint256, Transaction>();
        private Queue<uint256> _EvictionQueue = new Queue<uint256>();
        private ReaderWriterLock @lock = new ReaderWriterLock();
        public CachedTransactionRepository(ITransactionRepository inner)
        {
            if(inner == null)
                throw new ArgumentNullException("inner");
            this.ReadThrough = true;
            this.WriteThrough = true;
            this._Inner = inner;
            this.MaxCachedTransactions = 100;
        }

        public int MaxCachedTransactions
        {
            get;
            set;
        }

        public Transaction GetFromCache(uint256 txId)
        {
            using(this.@lock.LockRead())
            {
                return this._Transactions.TryGet(txId);
            }
        }

        #region ITransactionRepository Members

        public async Task<Transaction> GetAsync(uint256 txId)
        {
            bool found = false;
            Transaction result = null;
            using(this.@lock.LockRead())
            {
                found = this._Transactions.TryGetValue(txId, out result);
            }
            if(!found)
            {
                result = await this._Inner.GetAsync(txId).ConfigureAwait(false);
                if(this.ReadThrough)
                {
                    using(this.@lock.LockWrite())
                    {
                        this._Transactions.AddOrReplace(txId, result);
                        EvictIfNecessary(txId);
                    }
                }
            }
            return result;

        }

        private void EvictIfNecessary(uint256 txId)
        {
            this._EvictionQueue.Enqueue(txId);
            while(this._Transactions.Count > this.MaxCachedTransactions && this._EvictionQueue.Count > 0) this._Transactions.Remove(this._EvictionQueue.Dequeue());
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            if(this.WriteThrough)
            {
                using(this.@lock.LockWrite())
                {

                    if(!this._Transactions.ContainsKey(txId))
                    {
                        this._Transactions.AddOrReplace(txId, tx);
                        EvictIfNecessary(txId);
                    }
                    else
                        this._Transactions[txId] = tx;
                }
            }
            return this._Inner.PutAsync(txId, tx);
        }

        #endregion

        public bool WriteThrough
        {
            get;
            set;
        }

        public bool ReadThrough
        {
            get;
            set;
        }
    }
}
