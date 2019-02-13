using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore.Controllers;
using Stratis.Bitcoin.Features.BlockStore.Pruning;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.BlockStore.Tests")]

namespace Stratis.Bitcoin.Features.BlockStore
{
    public class BlockStoreFeature : FullNodeFeature
    {
        private readonly Network network;
        private readonly ConcurrentChain chain;

        private readonly BlockStoreSignaled blockStoreSignaled;

        private readonly IConnectionManager connectionManager;

        private readonly StoreSettings storeSettings;

        private readonly IChainState chainState;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        private readonly IBlockStoreQueue blockStoreQueue;

        private readonly IConsensusManager consensusManager;

        private readonly ICheckpoints checkpoints;

        private readonly IPrunedBlockRepository prunedBlockRepository;

        public BlockStoreFeature(
            Network network,
            ConcurrentChain chain,
            IConnectionManager connectionManager,
            BlockStoreSignaled blockStoreSignaled,
            ILoggerFactory loggerFactory,
            StoreSettings storeSettings,
            IChainState chainState,
            IBlockStoreQueue blockStoreQueue,
            INodeStats nodeStats,
            IConsensusManager consensusManager,
            ICheckpoints checkpoints,
            IPrunedBlockRepository prunedBlockRepository)
        {
            this.network = network;
            this.chain = chain;
            this.blockStoreQueue = blockStoreQueue;
            this.blockStoreSignaled = blockStoreSignaled;
            this.connectionManager = connectionManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.storeSettings = storeSettings;
            this.chainState = chainState;
            this.consensusManager = consensusManager;
            this.checkpoints = checkpoints;
            this.prunedBlockRepository = prunedBlockRepository;

            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 900);
        }

        private void AddInlineStats(StringBuilder log)
        {
            ChainedHeader highestBlock = this.chainState.BlockStoreTip;

            if (highestBlock != null)
            {
                var builder = new StringBuilder();
                builder.Append("BlockStore.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + highestBlock.Height.ToString().PadRight(8));
                builder.Append(" BlockStore.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + highestBlock.HashBlock);
                log.AppendLine(builder.ToString());
            }
        }

        public override async Task InitializeAsync()
        {
            this.prunedBlockRepository.InitializeAsync().GetAwaiter().GetResult();

            if (!this.storeSettings.PruningEnabled && this.prunedBlockRepository.PrunedTip != null)
                throw new BlockStoreException("The node cannot start as it has been previously pruned, please clear the data folders and resync.");

            if (this.storeSettings.PruningEnabled)
            {
                if (this.storeSettings.AmountOfBlocksToKeep < this.network.Consensus.MaxReorgLength)
                    throw new BlockStoreException($"The amount of blocks to prune [{this.storeSettings.AmountOfBlocksToKeep}] (blocks to keep) cannot be less than the node's max reorg length of {this.network.Consensus.MaxReorgLength}.");

                this.logger.LogInformation("Pruning BlockStore...");
                await this.prunedBlockRepository.PruneAndCompactDatabase(this.chainState.BlockStoreTip, this.network, true);
            }

            // Use ProvenHeadersBlockStoreBehavior for PoS Networks
            if (this.network.Consensus.IsProofOfStake)
            {
                this.connectionManager.Parameters.TemplateBehaviors.Add(new ProvenHeadersBlockStoreBehavior(this.network, this.chain, this.chainState, this.loggerFactory, this.consensusManager, this.checkpoints, this.blockStoreQueue));
            }
            else
            {
                this.connectionManager.Parameters.TemplateBehaviors.Add(new BlockStoreBehavior(this.chain, this.chainState, this.loggerFactory, this.consensusManager, this.blockStoreQueue));
            }

            // Signal to peers that this node can serve blocks.
            // TODO: Add NetworkLimited which is what BTC uses for pruned nodes.
            this.connectionManager.Parameters.Services = (this.storeSettings.PruningEnabled ? NetworkPeerServices.Nothing : NetworkPeerServices.Network);

            // Temporary measure to support asking witness data on BTC.
            // At some point NetworkPeerServices will move to the Network class,
            // Then this values should be taken from there.
            if (!this.network.Consensus.IsProofOfStake)
                this.connectionManager.Parameters.Services |= NetworkPeerServices.NODE_WITNESS;

            this.blockStoreSignaled.Initialize();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (this.storeSettings.PruningEnabled)
            {
                this.logger.LogInformation("Pruning BlockStore...");
                this.prunedBlockRepository.PruneAndCompactDatabase(this.chainState.BlockStoreTip, this.network, false);
            }

            this.logger.LogInformation("Stopping BlockStore.");

            this.blockStoreSignaled.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderBlockStoreExtension
    {
        public static IFullNodeBuilder UseBlockStore(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<BlockStoreFeature>("db");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<BlockStoreFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<IBlockStoreQueue, BlockStoreQueue>().AddSingleton<IBlockStore>(provider => provider.GetService<IBlockStoreQueue>());
                        services.AddSingleton<IBlockRepository, BlockRepository>();
                        services.AddSingleton<IPrunedBlockRepository, PrunedBlockRepository>();

                        if (fullNodeBuilder.Network.Consensus.IsProofOfStake)
                            services.AddSingleton<BlockStoreSignaled, ProvenHeadersBlockStoreSignaled>();
                        else
                            services.AddSingleton<BlockStoreSignaled>();

                        services.AddSingleton<StoreSettings>();
                        services.AddSingleton<BlockStoreController>();
                        services.AddSingleton<IBlockStoreQueueFlushCondition, BlockStoreQueueFlushCondition>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
