using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Mining;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Provides an ability to mine or stake.
    /// </summary>
    public class MiningFeature : FullNodeFeature
    {
        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Settings relevant to mining or staking.</summary>
        private readonly MinerSettings minerSettings;

        /// <summary>Settings relevant to node.</summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>POW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>POS staker.</summary>
        private readonly IPosMinting posMinting;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>State of time synchronization feature that stores collected data samples.</summary>
        private readonly ITimeSyncBehaviorState timeSyncBehaviorState;

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="minerSettings">Settings relevant to mining or staking.</param>
        /// <param name="nodeSettings">The node's configuration settings.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="timeSyncBehaviorState">State of time synchronization feature that stores collected data samples.</param>
        /// <param name="powMining">POW miner.</param>
        /// <param name="posMinting">POS staker.</param>
        public MiningFeature(
            Network network,
            MinerSettings minerSettings,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            ITimeSyncBehaviorState timeSyncBehaviorState,
            IPowMining powMining,
            IPosMinting posMinting = null)
        {
            this.network = network;
            this.minerSettings = minerSettings;
            this.nodeSettings = nodeSettings;
            this.powMining = powMining;
            this.timeSyncBehaviorState = timeSyncBehaviorState;
            this.posMinting = posMinting;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            MinerSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            MinerSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <summary>
        /// Starts staking a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="walletPassword">The password of the wallet.</param>
        public void StartStaking(string walletName, string walletPassword)
        {
            if (this.timeSyncBehaviorState.IsSystemTimeOutOfSync)
            {
                string errorMessage = "Staking cannot start, your system time does not match that of other nodes on the network." + Environment.NewLine
                                    + "Please adjust your system time and restart the node.";
                this.logger.LogError(errorMessage);
                throw new ConfigurationException(errorMessage);
            }

            if (!string.IsNullOrEmpty(walletName) && !string.IsNullOrEmpty(walletPassword))
            {
                this.logger.LogInformation("Staking enabled on wallet '{0}'.", walletName);

                this.posMinting.Stake(new WalletSecret
                {
                    WalletPassword = walletPassword,
                    WalletName = walletName
                });
            }
            else
            {
                string errorMessage = "Staking not started, wallet name or password were not provided.";
                this.logger.LogError(errorMessage);
                throw new ConfigurationException(errorMessage);
            }
        }

        /// <summary>
        /// Stop a staking wallet.
        /// </summary>
        public void StopStaking()
        {
            this.posMinting?.StopStake();
            this.logger.LogInformation("Staking stopped.");
        }

        /// <summary>
        /// Stop a Proof of Work miner.
        /// </summary>
        public void StopMining()
        {
            this.powMining?.StopMining();
            this.logger.LogInformation("Mining stopped.");
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            if (this.minerSettings.Mine)
            {
                string mineToAddress = this.minerSettings.MineAddress;
                // if (string.IsNullOrEmpty(mineToAddress)) ;
                //    TODO: get an address from the wallet.

                if (!string.IsNullOrEmpty(mineToAddress))
                {
                    this.logger.LogInformation("Mining enabled.");

                    this.powMining.Mine(BitcoinAddress.Create(mineToAddress, this.network).ScriptPubKey);
                }
            }

            if (this.minerSettings.Stake)
            {
                this.StartStaking(this.minerSettings.WalletName, this.minerSettings.WalletPassword);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.StopMining();
            this.StopStaking();
        }

        /// <inheritdoc />
        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            if (services.ServiceProvider.GetService<IPosMinting>() != null)
            {
                services.Features.EnsureFeature<WalletFeature>();
            }

            // Mining and staking require block store feature.
            if (this.minerSettings.Mine || this.minerSettings.Stake)
            {
                services.Features.EnsureFeature<BlockStoreFeature>();
                var blockSettings = services.ServiceProvider.GetService<StoreSettings>();
                if (blockSettings.Prune)
                    throw new ConfigurationException("BlockStore prune mode is incompatible with mining and staking.");
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMiningExtension
    {
        /// <summary>
        /// Adds a mining feature to the node being initialized.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="setup">Callback routine to be called when miner settings are loaded.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<RPCFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<MiningRpcController>();
                        services.AddSingleton<MiningController>();
                        services.AddSingleton<MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds POW and POS miner components to the node, so that it can mine or stake.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <param name="setup">Callback routine to be called when miner settings are loaded.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddPowPosMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<RPCFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IPosMinting, PosMinting>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosPowBlockDefinition>();
                        services.AddSingleton<StakingRpcController>();
                        services.AddSingleton<StakingController>();
                        services.AddSingleton<MiningRpcController>();
                        services.AddSingleton<MiningController>();
                        services.AddSingleton<MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}