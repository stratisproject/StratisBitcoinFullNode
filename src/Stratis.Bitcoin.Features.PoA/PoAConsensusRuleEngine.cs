using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <inheritdoc />
    public class PoAConsensusRuleEngine : PowConsensusRuleEngine
    {
        public SlotsManager SlotsManager { get; private set; }

        public PoABlockHeaderValidator poaHeaderValidator { get; private set; }

        public PoAConsensusRuleEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain,
            NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, SlotsManager slotsManager, PoABlockHeaderValidator poaHeaderValidator)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState, invalidBlockHashStore, nodeStats)
        {
            this.SlotsManager = slotsManager;
            this.poaHeaderValidator = poaHeaderValidator;
        }

        public override ValidationContext HeaderValidation(ChainedHeader header)
        {
            ValidationContext result = base.HeaderValidation(header);

            if ((result.Error == PoAConsensusErrors.InvalidHeaderSignature) &&
                ((PoAConsensusOptions) this.Network.Consensus.Options).VotingEnabled &&
                header.Height > this.ChainState.ConsensusTip.Height + 1)
            {
                result.InsufficientHeaderInformation = true;
            }

            return result;
        }
    }
}
