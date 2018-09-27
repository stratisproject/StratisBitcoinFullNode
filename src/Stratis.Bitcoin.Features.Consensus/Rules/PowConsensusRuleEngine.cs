using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
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
        public ICoinView UtxoSet { get; }

        private readonly CoinviewPrefetcher prefetcher;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public PowConsensusRuleEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain,
            NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, NodeSettings nodeSettings)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, chainState, invalidBlockHashStore, nodeStats, nodeSettings)
        {
            this.UtxoSet = utxoSet;
            this.prefetcher = new CoinviewPrefetcher(this.UtxoSet, chain, loggerFactory);
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
        public override Task Initialize()
        {
            return ((DBreezeCoinView)((CachedCoinView)this.UtxoSet).Inner).InitializeAsync();
        }

        public override async Task<ValidationContext> FullValidationAsync(ChainedHeader header, Block block)
        {
            ValidationContext result = await base.FullValidationAsync(header, block).ConfigureAwait(false);

            if ((result != null) && (result.Error == null))
            {
                // Notify prefetch manager about block that was validated so prefetch manager
                // can decide what coins we will most likely need for full validation in the near future.
                this.prefetcher.Prefetch(header);
            }

            return result;
        }

        public override void Dispose()
        {
            this.prefetcher.Dispose();

            var cache = this.UtxoSet as CachedCoinView;
            if (cache != null)
            {
                this.logger.LogInformation("Flushing Cache CoinView.");
                cache.FlushAsync().GetAwaiter().GetResult();
                cache.Dispose();
            }

            ((DBreezeCoinView)((CachedCoinView)this.UtxoSet).Inner).Dispose();
        }
    }
}