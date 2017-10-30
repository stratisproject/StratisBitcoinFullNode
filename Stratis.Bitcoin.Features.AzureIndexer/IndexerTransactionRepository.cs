using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class IndexerTransactionRepository : ITransactionRepository
    {
        private readonly IndexerConfiguration _Configuration;
        public IndexerConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }
        public IndexerTransactionRepository(IndexerConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            _Configuration = config;
        }
        #region ITransactionRepository Members

        public async Task<Transaction> GetAsync(uint256 txId)
        {
            var tx = await _Configuration.CreateIndexerClient().GetTransactionAsync(false, txId).ConfigureAwait(false);
            if (tx == null)
                return null;
            return tx.Transaction;
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            _Configuration.CreateIndexer().Index(new TransactionEntry.Entity(txId, tx, null));
            return Task.FromResult(false);
        }

        #endregion
    }
}
