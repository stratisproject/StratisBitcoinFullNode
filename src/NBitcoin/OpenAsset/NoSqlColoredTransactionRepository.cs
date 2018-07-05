using System.Threading.Tasks;

namespace NBitcoin.OpenAsset
{
    public class NoSqlColoredTransactionRepository : IColoredTransactionRepository
    {
        public NoSqlColoredTransactionRepository()
            : this(null, null)
        {
        }

        public NoSqlColoredTransactionRepository(ITransactionRepository transactionRepository, NoSqlRepository repository)
        {
            if(transactionRepository == null)
                transactionRepository = new NoSqlTransactionRepository(repository.Network);

            if(repository == null)
                repository = new InMemoryNoSqlRepository(repository.Network);

            this.Transactions = transactionRepository;
            this.Repository = repository;
        }

        public NoSqlRepository Repository { get; }
        public ITransactionRepository Transactions { get; }

        public Task<ColoredTransaction> GetAsync(uint256 txId)
        {
            return this.Repository.GetAsync<ColoredTransaction>(GetId(txId));
        }

        private static string GetId(uint256 txId)
        {
            return "ctx-" + txId;
        }

        public Task PutAsync(uint256 txId, ColoredTransaction tx)
        {
            return this.Repository.PutAsync(GetId(txId), tx);
        }
    }
}