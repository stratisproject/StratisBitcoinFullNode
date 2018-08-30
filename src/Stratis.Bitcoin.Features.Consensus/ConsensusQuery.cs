using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
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
            CoinViews.FetchCoinsResponse response = null;

            if (this.coinView != null)
            {
                response = await this.coinView.FetchCoinsAsync(new[] { trxid }).ConfigureAwait(false);
            }

            return response?.UnspentOutputs?.SingleOrDefault();
        }
    }
}
