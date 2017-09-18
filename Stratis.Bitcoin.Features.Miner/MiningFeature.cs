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
                var minto = this.minerSettings.MineAddress;
                if (string.IsNullOrEmpty(minto)) ;
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
                    this.logger.LogWarning("Staking not started, wallet name or password where not provided.");
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
