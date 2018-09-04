using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.ColdStaking.Controllers;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// Feature for cold staking which eliminates the need to keep the coins in the hot wallet.
    /// </summary>
    /// <remarks>
    /// <para>In order to produce blocks on Stratis network, a miner has to be online with running
    /// node and have its wallet open. This is necessary because at each time slot, the miner is
    /// supposed to check whether one of its UTXOs is eligible to be used as so-called coinstake kernel
    /// input and if so, it needs to use the private key associated with this UTXO in order to produce
    /// the coinstake transaction.</para>
    /// <para>The chance of a UTXO being eligible for producing a coinstake transaction grows linearly
    /// with the number of coins that the UTXO presents. This implies that the biggest miners on the
    /// network are required to keep the coins in a hot wallet. This is dangerous in case the machine
    /// where the hot wallet runs is compromised.</para>
    /// <para>We propose cold staking, which is mechanism that eliminates the need to keep the coins
    /// in the hot wallet. With cold staking implemented, the miner still needs to be online and running
    /// a node with an open wallet, but the coins that are used for staking, can be safely stored in cold
    /// storage. Therefore the open hot wallet does not need to hold any significant amount of coins, or
    /// it can even be completely empty.</para>
    /// </remarks>
    /// <seealso cref="<see cref="ColdStakingManager.GetColdStakingScript(NBitcoin.ScriptId, NBitcoin.ScriptId)"/>"/>
    /// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
    public class ColdStakingFeature : FullNodeFeature
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IWalletManager walletManager;

        private readonly ColdStakingManager coldStakingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColdStakingFeature"/> class.
        /// </summary>
        /// <param name="coldStakingManager">The cold staking manager.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public ColdStakingFeature(
            ColdStakingManager coldStakingManager,
            IWalletManager walletManager,
            ILoggerFactory loggerFactory)
        {
            this.coldStakingManager = coldStakingManager;
            this.walletManager = walletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
        }

        public override void Initialize()
        {
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderLightWalletExtension
    {
        public static IFullNodeBuilder UseColdStaking(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ColdStakingFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<ColdStakingManager>();
                        services.AddSingleton<ColdStakingController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
