using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS
{
    public static partial class IFullNodeBuilderExtensions
    {

        /// <summary>
        /// Configures the node with the smart contract proof of stake consensus model.
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPosConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<DBreezeCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<StakeChainStore>().AddSingleton<IStakeChain, StakeChainStore>(provider => provider.GetService<StakeChainStore>());
                        services.AddSingleton<IStakeValidator, StakeValidator>();
                        services.AddSingleton<ConsensusController>();

                        services.AddSingleton<PosConsensusRuleEngine>();
                        services.AddSingleton<IConsensusRuleEngine>(f =>
                        {
                            var concreteRuleEngine = f.GetService<PosConsensusRuleEngine>();
                            var ruleRegistration = f.GetService<IRuleRegistration>();

                            return new DiConsensusRuleEngine(concreteRuleEngine, ruleRegistration);
                        });
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds mining to the smart contract node.
        /// <para>We inject <see cref="IPowMining"/> with a smart contract block provider and definition.</para>
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPosPowMining(this IFullNodeBuilder fullNodeBuilder)
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
                        services.AddSingleton<BlockDefinition, SmartContractPosPowBlockDefinition>();
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();
                        services.AddSingleton<MiningRpcController>();
                        services.AddSingleton<MiningController>();
                        services.AddSingleton<StakingController>();
                        services.AddSingleton<StakingRpcController>();
                        services.AddSingleton<MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
