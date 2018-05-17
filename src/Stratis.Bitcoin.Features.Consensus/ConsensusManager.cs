using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusManager : INetworkDifficulty, IGetUnspentTransaction
    {
        public IConsensusLoop ConsensusLoop { get; private set; }

        public IDateTimeProvider DateTimeProvider { get; private set; }

        public NodeSettings NodeSettings { get; private set; }

        public Network Network { get; private set; }


        public ConsensusManager(IConsensusLoop consensusLoop = null, IDateTimeProvider dateTimeProvider = null, NodeSettings nodeSettings = null, Network network = null)
        {
            this.ConsensusLoop = consensusLoop;
            this.DateTimeProvider = dateTimeProvider;
            this.NodeSettings = nodeSettings;
            this.Network = network;
        }

        public Target GetNetworkDifficulty()
        {
            var powCoinviewRule = this.ConsensusLoop.ConsensusRules.GetRule<PowCoinviewRule>();
            if ((powCoinviewRule.ConsensusParams != null) && (this.ConsensusLoop?.Tip != null))
                return this.ConsensusLoop?.Tip?.GetWorkRequired(powCoinviewRule.ConsensusParams);

            return null;
        }

        /// <inheritdoc />
        public async Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            CoinViews.FetchCoinsResponse response = null;
            if (this.ConsensusLoop?.UTXOSet != null)
                response = await this.ConsensusLoop.UTXOSet.FetchCoinsAsync(new[] { trxid }).ConfigureAwait(false);
            return response?.UnspentOutputs?.SingleOrDefault();
        }
    }
}
