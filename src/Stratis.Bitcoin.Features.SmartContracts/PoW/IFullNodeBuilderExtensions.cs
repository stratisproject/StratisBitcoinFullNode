using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts.PoS;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.SmartContracts.PoW
{
    public static partial class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Configures the node with the smart contract proof of work consensus model.
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPowConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ConsensusFeature>()
                .DependOn<SmartContractFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<ConsensusOptions, ConsensusOptions>();
                    services.AddSingleton<DBreezeCoinView>();
                    services.AddSingleton<ICoinView, CachedCoinView>();
                    services.AddSingleton<ConsensusController>();
                    services.AddSingleton<IConsensusRuleEngine, SmartContractPowConsensusRuleEngine>();

                    new SmartContractPowRuleRegistration(fullNodeBuilder.Network).RegisterRules(fullNodeBuilder.Network.Consensus);
                });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds mining to the smart contract node.
        /// <para>We inject <see cref="IPowMining"/> with a smart contract block provider and definition.</para>
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPowMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<RPCFeature>()
                    .DependOn<SmartContractWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, SmartContractBlockProvider>();
                        services.AddSingleton<BlockDefinition, SmartContractBlockDefinition>();
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();
                        services.AddSingleton<MiningController>();
                        services.AddSingleton<MiningRpcController>();
                        services.AddSingleton<MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
