using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// Extension of consensus rules that provide access to a store based on UTXO (Unspent transaction outputs).
    /// </summary>
    public class PowConsensusRules : ConsensusRules
    {
        /// <summary>The consensus db, containing all unspent UTXO in the chain.</summary>
        public CoinView UtxoSet { get; }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public PowConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, CoinView utxoSet, IChainState chainState)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, chainState)
        {
            this.UtxoSet = utxoSet;
        }

        /// <inheritdoc />
        public override RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip)
        {
            return new PowRuleContext(validationContext, this.DateTimeProvider.GetTimeOffset());
        }

        /// <inheritdoc />
        public override Task<uint256> GetBlockHashAsync()
        {
            return this.UtxoSet.GetBlockHashAsync();
        }

        /// <inheritdoc />
        public override async Task<RewindState> RewindAsync()
        {
            return new RewindState()
            {
                BlockHash = await this.UtxoSet.Rewind().ConfigureAwait(false)
            };
        }

        /// <inheritdoc />
        public override Task Initialize()
        {
            return ((DBreezeCoinView)((CachedCoinView)this.UtxoSet).Inner).InitializeAsync();
        }
    }

    /// <summary>
    /// Extension of consensus rules that provide access to a PoS store.
    /// </summary>
    /// <remarks>
    /// A Proof-Of-Stake blockchain as implemented in this code base represents a hybrid POS/POW consensus model.
    /// </remarks>
    public class PosConsensusRules : PowConsensusRules
    {
        /// <summary>Database of stake related data for the current blockchain.</summary>
        public IStakeChain StakeChain { get; }

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        public IStakeValidator StakeValidator { get; }

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public PosConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, CoinView utxoSet, IStakeChain stakeChain, IStakeValidator stakeValidator, IChainState chainState)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState)
        {
            this.StakeChain = stakeChain;
            this.StakeValidator = stakeValidator;
        }

        /// <inheritdoc />
        public override RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip)
        {
            return new PosRuleContext(validationContext, this.DateTimeProvider.GetTimeOffset());
        }

        /// <inheritdoc />
        public override async Task Initialize()
        {
            await base.Initialize().ConfigureAwait(false);

            await this.StakeChain.LoadAsync();
        }
    }
}
