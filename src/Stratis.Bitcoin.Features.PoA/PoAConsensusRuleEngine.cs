using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <inheritdoc />
    public class PoAConsensusRuleEngine : PowConsensusRuleEngine
    {
        public SlotsManager SlotsManager { get; private set; }

        public PoABlockHeaderValidator PoaHeaderValidator { get; private set; }

        public VotingManager VotingManager { get; private set; }

        public FederationManager FederationManager { get; private set; }

        public PoAConsensusRuleEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain,
            NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, SlotsManager slotsManager, PoABlockHeaderValidator poaHeaderValidator,
            VotingManager votingManager, FederationManager federationManager)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState, invalidBlockHashStore, nodeStats)
        {
            this.SlotsManager = slotsManager;
            this.PoaHeaderValidator = poaHeaderValidator;
            this.VotingManager = votingManager;
            this.FederationManager = federationManager;
        }
    }
}
