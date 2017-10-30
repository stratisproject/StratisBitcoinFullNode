using NBitcoin.OpenAsset;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    internal class ReadOnlyTransactionRepository : ITransactionRepository
    {
        private NoSqlTransactionRepository cache;

        public ReadOnlyTransactionRepository(NoSqlTransactionRepository cache)
        {
            this.cache = cache;
        }
        #region ITransactionRepository Members

        public Task<Transaction> GetAsync(uint256 txId)
        {
            return cache.GetAsync(txId);
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return Task.FromResult(false);
        }

        #endregion
    }
    internal class CompositeTransactionRepository : ITransactionRepository
    {
        public CompositeTransactionRepository(ITransactionRepository[] repositories)
        {
            _Repositories = repositories.ToArray();
        }
        ITransactionRepository[] _Repositories;
        #region ITransactionRepository Members

        public async Task<Transaction> GetAsync(uint256 txId)
        {
            foreach(var repo in _Repositories)
            {
                var result = await repo.GetAsync(txId).ConfigureAwait(false);
                if(result != null)
                    return result;
            }
            return null;
        }

        public async Task PutAsync(uint256 txId, Transaction tx)
        {
            foreach(var repo in _Repositories)
            {
                await repo.PutAsync(txId, tx).ConfigureAwait(false);
            }
        }

        #endregion
    }
    public class IndexerColoredTransactionRepository : IColoredTransactionRepository
    {
        private readonly IndexerConfiguration _Configuration;
        public IndexerConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }

        public IndexerColoredTransactionRepository(IndexerConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            _Configuration = config;
            _Transactions = new IndexerTransactionRepository(config);
        }

        #region IColoredTransactionRepository Members

        public async Task<ColoredTransaction> GetAsync(uint256 txId)
        {
            var client = _Configuration.CreateIndexerClient();
            var tx = await client.GetTransactionAsync(false, false, txId).ConfigureAwait(false);
            if (tx == null)
                return null;
            return tx.ColoredTransaction;
        }

        public Task PutAsync(uint256 txId, ColoredTransaction colored)
        {
            _Configuration.CreateIndexer().Index(new TransactionEntry.Entity(txId, colored));
            return Task.FromResult(false);
        }

        ITransactionRepository _Transactions;
        public ITransactionRepository Transactions
        {
            get
            {
                return _Transactions;
            }
            set
            {
                _Transactions = value;
            }
        }

        #endregion
    }
}
