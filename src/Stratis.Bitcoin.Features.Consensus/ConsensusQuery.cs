using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    /// <summary>
    /// A class that provides the ability to query consensus elements.
    /// </summary>
    public class ConsensusQuery : IGetUnspentTransaction, INetworkDifficulty
    {
        private readonly IChainState chainState;
        private readonly ICoinView coinView;
        private readonly ILogger logger;
        private readonly Network network;

        public ConsensusQuery(
            ICoinView coinView,
            IChainState chainState,
            Network network,
            ILoggerFactory loggerFactory)
        {
            this.coinView = coinView;
            this.chainState = chainState;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            FetchCoinsResponse response = this.coinView.FetchCoins(new[] { trxid });

            UnspentOutputs unspentOutputs = response.UnspentOutputs.FirstOrDefault();

            return Task.FromResult(unspentOutputs);
        }

        /// <inheritdoc/>
        public Target GetNetworkDifficulty()
        {
            return this.chainState.ConsensusTip?.GetWorkRequired(this.network.Consensus);
        }
    }
}