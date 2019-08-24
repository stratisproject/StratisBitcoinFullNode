using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    /// <inheritdoc />
    public class PoAConsensusRuleEngine : PowConsensusRuleEngine
    {
        public ISlotsManager SlotsManager { get; private set; }

        public PoABlockHeaderValidator PoaHeaderValidator { get; private set; }

        public VotingManager VotingManager { get; private set; }

        public IFederationManager FederationManager { get; private set; }

        public PoAConsensusRuleEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ChainIndexer chainIndexer,
            NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, ISlotsManager slotsManager, PoABlockHeaderValidator poaHeaderValidator,
            VotingManager votingManager, IFederationManager federationManager, IAsyncProvider asyncProvider, ConsensusRulesContainer consensusRulesContainer)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState, invalidBlockHashStore, nodeStats, asyncProvider, consensusRulesContainer)
        {
            this.SlotsManager = slotsManager;
            this.PoaHeaderValidator = poaHeaderValidator;
            this.VotingManager = votingManager;
            this.FederationManager = federationManager;
        }
    }
}
