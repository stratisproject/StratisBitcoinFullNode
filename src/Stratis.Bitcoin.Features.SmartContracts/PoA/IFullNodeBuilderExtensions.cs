using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.ContractSigning;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public static partial class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Configures the node with the smart contract proof of authority consensus model.
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPoAConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .DependOn<SmartContractFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<DBreezeCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<ConsensusController>();
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();
                        services.AddSingleton<IPollResultExecutor, PollResultExecutor>();

                        services.AddSingleton<PoAConsensusRuleEngine>();
                        services.AddSingleton<IRuleRegistration, SmartContractPoARuleRegistration>();
                        services.AddSingleton<IConsensusRuleEngine>(f =>
                        {
                            var concreteRuleEngine = f.GetService<PoAConsensusRuleEngine>();
                            var ruleRegistration = f.GetService<IRuleRegistration>();

                            return new DiConsensusRuleEngine(concreteRuleEngine, ruleRegistration);
                        });
                    });
            });

            return fullNodeBuilder;
        }

        public static SmartContractOptions UseSignedContracts(this SmartContractOptions options)
        {           
            IServiceCollection services = options.Services;
            var networkWithPubKey = (ISignedCodePubKeyHolder) options.Network;

            // Replace serializer
            services.RemoveAll<ICallDataSerializer>();
            services.AddSingleton<ICallDataSerializer, SignedCodeCallDataSerializer>();
            services.AddSingleton<IContractTransactionValidationLogic>(f => new ContractSignedCodeLogic(new ContractSigner(), networkWithPubKey.SigningContractPubKey));

            return options;
        }

        public static IFullNodeBuilder UseContractWhitelist(this IFullNodeBuilder fullNodeBuilder)
        {
            if(fullNodeBuilder.Features.FeatureRegistrations.All(f => f.FeatureType != typeof(PoAFeature)))
            {
                throw new InvalidOperationException("PoAFeature must be registered to use contract whitelist!");
            }

            if (fullNodeBuilder.Features.FeatureRegistrations.All(f => f.FeatureType != typeof(SmartContractFeature)))
            {
                throw new InvalidOperationException("SmartContractFeature must be registered to use contract whitelist!");
            }

            IServiceCollection services = fullNodeBuilder.Services;

            services.AddSingleton<IContractCodeHashingStrategy, Sha256CodeHashingStrategy>();
            services.AddSingleton<IWhitelistedHashChecker, WhitelistedHashChecker>();
            services.AddSingleton<IContractTransactionValidationLogic, AllowedCodeHashLogic>();

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds mining to the smart contract node when on a proof-of-authority network.
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPoAMining(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PoAFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<PoAMinerSettings>();
                        services.AddSingleton<MinerSettings>();
                        services.AddSingleton<SlotsManager>();
                        services.AddSingleton<BlockDefinition, SmartContractPoABlockDefinition>();
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
