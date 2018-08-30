using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// A class that provides the ability to query consensus elements.
    /// </summary>
    public class ConsensusQuery : IGetUnspentTransaction
    {
        private readonly ICoinView coinView;
        private readonly ILogger logger;
        
        public ConsensusQuery(
            ICoinView coinView,
            ILoggerFactory loggerFactory)
        {
            this.coinView = coinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public async Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);

            FetchCoinsResponse response = await this.coinView.FetchCoinsAsync(new[] { trxid }).ConfigureAwait(false);

            UnspentOutputs unspentOutputs = response.UnspentOutputs.FirstOrDefault();

            this.logger.LogTrace("(-):{0}", unspentOutputs);
            return unspentOutputs;
        }
    }
}