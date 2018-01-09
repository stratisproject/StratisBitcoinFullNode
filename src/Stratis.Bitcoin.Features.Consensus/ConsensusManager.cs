using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
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

        public IPowConsensusValidator ConsensusValidator { get; private set; }

        public ConsensusManager(IConsensusLoop consensusLoop = null, IDateTimeProvider dateTimeProvider = null, NodeSettings nodeSettings = null, Network network = null,
            IPowConsensusValidator consensusValidator = null)
        {
            this.ConsensusLoop = consensusLoop;
            this.DateTimeProvider = dateTimeProvider;
            this.NodeSettings = nodeSettings;
            this.Network = network;
            this.ConsensusValidator = consensusValidator;
        }

        public Target GetNetworkDifficulty()
        {
            if ((this.ConsensusValidator?.ConsensusParams != null) && (this.ConsensusLoop?.Tip != null))
                return this.ConsensusLoop?.Tip?.GetWorkRequired(this.ConsensusValidator.ConsensusParams);
            else
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
