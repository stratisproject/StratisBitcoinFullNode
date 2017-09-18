using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System;

namespace Stratis.Bitcoin.Features.Miner
{
    public class MiningFeature : FullNodeFeature
    {
        private readonly Network network;
        private readonly MinerSettings minerSettings;
        private readonly PowMining powMining;
        private readonly PosMinting posMinting;
        private readonly WalletManager walletManager;
        private readonly ILogger logger;

        private IAsyncLoop posLoop;
        private IAsyncLoop powLoop;

        public MiningFeature(
            Network network, 
            MinerSettings minerSettings, 
            ILoggerFactory loggerFactory, 
            PowMining powMining, 
            PosMinting posMinting = null, 
            WalletManager walletManager = null)
        {
            this.network = network;
            this.minerSettings = minerSettings;
            this.powMining = powMining;
            this.posMinting = posMinting;
            this.walletManager = walletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        ///<inheritdoc />
        public override void Start()
        {
            if (this.minerSettings.Mine)
            {
                string minto = this.minerSettings.MineAddress;
                // if (string.IsNullOrEmpty(minto)) ;
                //	TODO: get an address from the wallet.

                if (!string.IsNullOrEmpty(minto))
                {
                    this.powLoop = this.powMining.Mine(BitcoinAddress.Create(minto, this.network).ScriptPubKey);
                }
            }

            if (this.minerSettings.Stake)
            {
                if (!string.IsNullOrEmpty(this.minerSettings.WalletName)
                    && !string.IsNullOrEmpty(this.minerSettings.WalletPassword))
                {
                    this.posLoop = this.posMinting.Mine(new PosMinting.WalletSecret()
                    {
                        WalletPassword = this.minerSettings.WalletPassword,
                        WalletName = this.minerSettings.WalletName
                    });
                }
                else
                {
                    this.logger.LogWarning("Staking not started, wallet name or password were not provided.");
                }
            }
        }

        ///<inheritdoc />
        public override void Stop()
        {
            this.powLoop?.Dispose();
            this.posLoop?.Dispose();
        }

        ///<inheritdoc />
        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {            
            if (services.ServiceProvider.GetService<PosMinting>() != null)
            {
                services.Features.EnsureFeature<WalletFeature>();
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
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
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<PowMining>();
                        services.AddSingleton<AssemblerFactory, PowAssemblerFactory>();
                        services.AddSingleton<MiningRPCController>();
                        services.AddSingleton<MinerSettings>(new MinerSettings(setup));

                    });
            });
            
            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds POW and POS miner components to the node, so that it can stake/mine.
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
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<PowMining>();
                        services.AddSingleton<PosMinting>();
                        services.AddSingleton<AssemblerFactory, PosAssemblerFactory>();
                        services.AddSingleton<MiningRPCController>();
                        services.AddSingleton<MinerSettings>(new MinerSettings(setup));
                    });
            });

            return fullNodeBuilder;
        }
    }
}
