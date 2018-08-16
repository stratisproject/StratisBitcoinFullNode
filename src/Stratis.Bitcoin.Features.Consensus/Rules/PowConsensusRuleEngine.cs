using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// Extension of consensus rules that provide access to a store based on UTXO (Unspent transaction outputs).
    /// </summary>
    public class PowConsensusRuleEngine : ConsensusRuleEngine
    {
        /// <summary>The consensus db, containing all unspent UTXO in the chain.</summary>
        public ICachedCoinView UtxoSet { get; }

        public PowConsensusRuleEngine(
            Network network,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider,
            ConcurrentChain chain,
            NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings,
            ICheckpoints checkpoints,
            ICachedCoinView utxoSet,
            IChainState chainState)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, chainState)
        {
            this.UtxoSet = utxoSet;
        }

        /// <inheritdoc />
        public override RuleContext CreateRuleContext(ValidationContext validationContext)
        {
            return new PowRuleContext(validationContext, this.DateTimeProvider.GetTimeOffset());
        }

        /// <inheritdoc />
        public override Task<uint256> GetBlockHashAsync()
        {
            return this.UtxoSet.GetTipHashAsync();
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
        public override async Task Initialize()
        {
            if (this.UtxoSet is IBackedCoinView backedCoinView)
            {
                await backedCoinView.CoinViewStorage.InitializeAsync().ConfigureAwait(false);
            }

            this.UtxoSet.Initialize();
        }

        public override void Dispose()
        {
            if (this.UtxoSet is IBackedCoinView backedCoinView)
            {
                backedCoinView.CoinViewStorage.Dispose();
            }

            this.UtxoSet.Dispose();
        }
    }
}