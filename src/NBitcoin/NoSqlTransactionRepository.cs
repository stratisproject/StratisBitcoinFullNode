using System;
using System.Threading.Tasks;

namespace NBitcoin
{
    public class NoSqlTransactionRepository : ITransactionRepository
    {
        public NoSqlRepository Repository { get; }

        public NoSqlTransactionRepository(Network network)
            :this(new InMemoryNoSqlRepository(network))
        {
        }

        public NoSqlTransactionRepository(NoSqlRepository repository)
        {
            this.Repository = repository ?? throw new ArgumentNullException("repository");
        }

        public Task<Transaction> GetAsync(uint256 txId)
        {
            return this.Repository.GetAsync<Transaction>(GetId(txId));
        }

        private string GetId(uint256 txId)
        {
            return "tx-" + txId.ToString();
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return this.Repository.PutAsync(GetId(txId), tx);
        }
    }
}