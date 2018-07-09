using System.Threading.Tasks;

namespace NBitcoin.OpenAsset
{
    public class NoSqlColoredTransactionRepository : IColoredTransactionRepository
    {
        public NoSqlColoredTransactionRepository(Network network)
            : this(network, null, null)
        {
        }

        public NoSqlColoredTransactionRepository(Network network, ITransactionRepository transactionRepository, NoSqlRepository noSqlRepository)
        {
            if(transactionRepository == null)
                transactionRepository = new NoSqlTransactionRepository(network);

            if(noSqlRepository == null)
                noSqlRepository = new InMemoryNoSqlRepository(network);

            this.Transactions = transactionRepository;
            this.Repository = noSqlRepository;
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