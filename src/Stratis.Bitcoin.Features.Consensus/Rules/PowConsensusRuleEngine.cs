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
using TracerAttributes;

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
        public PowConsensusRuleEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ChainIndexer chainIndexer,
            NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, chainState, invalidBlockHashStore, nodeStats)
        {
            this.UtxoSet = utxoSet;
            this.prefetcher = new CoinviewPrefetcher(this.UtxoSet, chainIndexer, loggerFactory);
        }

        /// <inheritdoc />
        [NoTrace]
        public override RuleContext CreateRuleContext(ValidationContext validationContext)
        {
            return new PowRuleContext(validationContext, this.DateTimeProvider.GetTimeOffset());
        }

        /// <inheritdoc />
        public override uint256 GetBlockHash()
        {
            return this.UtxoSet.GetTipHash();
        }

        /// <inheritdoc />
        public override Task<RewindState> RewindAsync()
        {
            var state = new RewindState()
            {
                BlockHash = this.UtxoSet.Rewind()
            };

            return Task.FromResult(state);
        }

        /// <inheritdoc />
        public override void Initialize(ChainedHeader chainTip)
        {
            var breezeCoinView = (DBreezeCoinView)((CachedCoinView)this.UtxoSet).Inner;

            breezeCoinView.Initialize();

            uint256 consensusTipHash = breezeCoinView.GetTipHash();

            while (true)
            {
                ChainedHeader pendingTip = chainTip.FindAncestorOrSelf(consensusTipHash);

                if (pendingTip != null)
                    break;

                this.logger.LogInformation("Rewinding coin db from {0}", consensusTipHash);
                // In case block store initialized behind, rewind until or before the block store tip.
                // The node will complete loading before connecting to peers so the chain will never know if a reorg happened.
                consensusTipHash = breezeCoinView.Rewind();
            }
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
                cache.Flush();
            }

            ((DBreezeCoinView)((CachedCoinView)this.UtxoSet).Inner).Dispose();
        }
    }
}