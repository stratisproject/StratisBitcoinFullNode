using System;
using System.Threading.Tasks;

namespace NBitcoin
{
    public class NoSqlTransactionRepository : ITransactionRepository
    {
        private readonly NoSqlRepository repository;
        public NoSqlRepository Repository
        {
            get
            {
                return repository;
            }
        }

        public NoSqlTransactionRepository(NetworkOptions options = null)
            :this(new InMemoryNoSqlRepository(options))
        {

        }
        public NoSqlTransactionRepository(NoSqlRepository repository)
        {
            if(repository == null)
                throw new ArgumentNullException("repository");
            this.repository = repository;
        }
        #region ITransactionRepository Members

        public Task<Transaction> GetAsync(uint256 txId)
        {
            return repository.GetAsync<Transaction>(GetId(txId));
        }

        private string GetId(uint256 txId)
        {
            return "tx-" + txId.ToString();
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return repository.PutAsync(GetId(txId), tx);
        }

        #endregion
    }
}
