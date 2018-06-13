using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBitcoin.OpenAsset
{
    public class CachedColoredTransactionRepository : IColoredTransactionRepository
    {
        private IColoredTransactionRepository _Inner;
        private CachedTransactionRepository _InnerTransactionRepository;
        private Dictionary<uint256, ColoredTransaction> _ColoredTransactions = new Dictionary<uint256, ColoredTransaction>();
        private Queue<uint256> _EvictionQueue = new Queue<uint256>();
        private ReaderWriterLock _lock = new ReaderWriterLock();

        public ColoredTransaction GetFromCache(uint256 txId)
        {
            using(this._lock.LockRead())
            {
                return this._ColoredTransactions.TryGet(txId);
            }
        }

        public int MaxCachedTransactions
        {
            get
            {
                return this._InnerTransactionRepository.MaxCachedTransactions;
            }
            set
            {
                this._InnerTransactionRepository.MaxCachedTransactions = value;
            }
        }

        public bool WriteThrough
        {
            get
            {
                return this._InnerTransactionRepository.WriteThrough;
            }
            set
            {
                this._InnerTransactionRepository.WriteThrough = value;
            }
        }

        public bool ReadThrough
        {
            get
            {
                return this._InnerTransactionRepository.ReadThrough;
            }
            set
            {
                this._InnerTransactionRepository.ReadThrough = value;
            }
        }

        public CachedColoredTransactionRepository(IColoredTransactionRepository inner)
        {
            if(inner == null)
                throw new ArgumentNullException("inner");
            this._Inner = inner;
            this._InnerTransactionRepository = new CachedTransactionRepository(inner.Transactions);
            this.MaxCachedTransactions = 1000;
        }
        #region IColoredTransactionRepository Members

        public CachedTransactionRepository Transactions
        {
            get
            {
                return this._InnerTransactionRepository;
            }
        }

        ITransactionRepository IColoredTransactionRepository.Transactions
        {
            get
            {
                return this._InnerTransactionRepository;
            }
        }

        private void EvictIfNecessary(uint256 txId)
        {
            this._EvictionQueue.Enqueue(txId);
            while(this._ColoredTransactions.Count > this.MaxCachedTransactions && this._EvictionQueue.Count > 0) this._ColoredTransactions.Remove(this._EvictionQueue.Dequeue());
        }

        public async Task<ColoredTransaction> GetAsync(uint256 txId)
        {
            ColoredTransaction result = null;
            bool found;
            using(this._lock.LockRead())
            {
                found = this._ColoredTransactions.TryGetValue(txId, out result);
            }
            if(!found)
            {
                result = await this._Inner.GetAsync(txId).ConfigureAwait(false);
                if(this.ReadThrough)
                {
                    using(this._lock.LockWrite())
                    {
                        this._ColoredTransactions.AddOrReplace(txId, result);
                        EvictIfNecessary(txId);
                    }
                }
            }
            return result;
        }

        public Task PutAsync(uint256 txId, ColoredTransaction tx)
        {

            if(this.WriteThrough)
            {
                using(this._lock.LockWrite())
                {

                    if(!this._ColoredTransactions.ContainsKey(txId))
                    {
                        this._ColoredTransactions.AddOrReplace(txId, tx);
                        EvictIfNecessary(txId);
                    }
                    else
                        this._ColoredTransactions[txId] = tx;
                }
            }
            return this._Inner.PutAsync(txId, tx);
        }

        #endregion
    }
}
