﻿using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

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

        /// <summary>POW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>POS staker.</summary>
        private readonly IPosMinting posMinting;

        /// <summary>Manager providing operations on wallets.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>POS staking loop.</summary>
        private IAsyncLoop posLoop;

        /// <summary>POW mining loop.</summary>
        private IAsyncLoop powLoop;

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="minerSettings">Settings relevant to mining or staking.</param>
        /// <param name="nodeSettings">The node's configuration settings.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="powMining">POW miner.</param>
        /// <param name="posMinting">POS staker.</param>
        /// <param name="walletManager">Manager providing operations on wallets.</param>
        public MiningFeature(
            Network network,
            MinerSettings minerSettings,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            IPowMining powMining,
            IPosMinting posMinting = null,
            IWalletManager walletManager = null)
        {
            this.network = network;
            this.minerSettings = minerSettings;
            this.minerSettings.Load(nodeSettings);
            this.powMining = powMining;
            this.posMinting = posMinting;
            this.walletManager = walletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Starts staking a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="walletPassword">The password of the wallet.</param>
        public void StartStaking(string walletName, string walletPassword)
        {
            if (!string.IsNullOrEmpty(walletName) && !string.IsNullOrEmpty(walletPassword))
            {
                this.logger.LogInformation("Staking enabled on wallet '{0}'.", walletName);

                this.posLoop = this.posMinting.Stake(new PosMinting.WalletSecret
                {
                    WalletPassword = walletPassword,
                    WalletName = walletName
                });
            }
            else
            {
                this.logger.LogWarning("Staking not started, wallet name or password were not provided.");
            }
        }

        /// <summary>
        /// Stop a staking wallet.
        /// </summary>
        public void StopStaking()
        {
            this.posMinting.StopStake();
            this.posLoop = null;
            this.logger.LogInformation("Staking stopped.");
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            if (this.minerSettings.Mine)
            {
                string minto = this.minerSettings.MineAddress;
                // if (string.IsNullOrEmpty(minto)) ;
                //    TODO: get an address from the wallet.

                if (!string.IsNullOrEmpty(minto))
                {
                    this.logger.LogInformation("Mining enabled.");

                    this.powLoop = this.powMining.Mine(BitcoinAddress.Create(minto, this.network).ScriptPubKey);
                }
            }

            if (this.minerSettings.Stake)
            {
                this.StartStaking(this.minerSettings.WalletName, this.minerSettings.WalletPassword);
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.powLoop?.Dispose();
            this.posLoop?.Dispose();
            this.posLoop = null;
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
                StoreSettings blockSettings = services.ServiceProvider.GetService<StoreSettings>();
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
        public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder, Action<MinerSettings> setup = null)
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
                        services.AddSingleton<AssemblerFactory, PowAssemblerFactory>();
                        services.AddSingleton<MinerController>();
                        services.AddSingleton<MiningRPCController>();
                        services.AddSingleton<MinerSettings>(new MinerSettings(setup));
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
        public static IFullNodeBuilder AddPowPosMining(this IFullNodeBuilder fullNodeBuilder, Action<MinerSettings> setup = null)
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
                        services.AddSingleton<AssemblerFactory, PosAssemblerFactory>();
                        services.AddSingleton<MinerController>();
                        services.AddSingleton<MiningRPCController>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<MinerSettings>(new MinerSettings(setup));
                    });
            });

            return fullNodeBuilder;
        }
    }
}
