using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using System;

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
        private readonly PowMining powMining;

        /// <summary>POS staker.</summary>
        private readonly PosMinting posMinting;

        /// <summary>Manager providing operations on wallets.</summary>
        private readonly WalletManager walletManager;

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
        /// <param name="loggerFactory">Factory to be used to create logger for the node.</param>
        /// <param name="powMining">POW miner.</param>
        /// <param name="posMinting">POS staker.</param>
        /// <param name="walletManager">Manager providing operations on wallets.</param>
        public MiningFeature(
            Network network, 
            MinerSettings minerSettings,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory, 
            PowMining powMining, 
            PosMinting posMinting = null, 
            WalletManager walletManager = null)
        {
            this.network = network;
            this.minerSettings = minerSettings;
            this.minerSettings.Load(nodeSettings);
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
                    this.logger.LogInformation("Mining enabled.");

                    this.powLoop = this.powMining.Mine(BitcoinAddress.Create(minto, this.network).ScriptPubKey);
                }
            }

            if (this.minerSettings.Stake)
            {
                if (!string.IsNullOrEmpty(this.minerSettings.WalletName)
                    && !string.IsNullOrEmpty(this.minerSettings.WalletPassword))
                {
                    this.logger.LogInformation("Staking enabled on wallet {0}.", this.minerSettings.WalletName);

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
