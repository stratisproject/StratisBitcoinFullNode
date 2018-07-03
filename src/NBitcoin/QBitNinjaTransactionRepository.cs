using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NBitcoin
{
    public class QBitNinjaTransactionRepository : ITransactionRepository
    {
        private readonly Uri baseUri;
        private readonly Network network;

        /// <summary>
        /// Use qbitninja public servers
        /// </summary>
        /// <param name="network"></param>
        public QBitNinjaTransactionRepository(Network network)
        {
            this.network = network ?? throw new ArgumentNullException("network");
            this.baseUri = new Uri("http://" + (network == Network.Main ? "" : "t") + "api.qbit.ninja/");
        }

        public QBitNinjaTransactionRepository(Uri baseUri)
            : this(baseUri.AbsoluteUri)
        {
        }

        public QBitNinjaTransactionRepository(string baseUri)
        {
            if(!baseUri.EndsWith("/"))
                baseUri += "/";

            this.baseUri = new Uri(baseUri, UriKind.Absolute);
        }

        public async Task<Transaction> GetAsync(uint256 txId)
        {
            using(var client = new HttpClient())
            {
                HttpResponseMessage tx = await client.GetAsync(this.baseUri.AbsoluteUri + "transactions/" + txId + "?format=raw").ConfigureAwait(false);
                if(tx.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                tx.EnsureSuccessStatusCode();

                byte[] bytes = await tx.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return this.network.Consensus.ConsensusFactory.CreateTransaction(bytes);
            }
        }

        public Task PutAsync(uint256 txId, Transaction tx)
        {
            return Task.FromResult(false);
        }
    }
}