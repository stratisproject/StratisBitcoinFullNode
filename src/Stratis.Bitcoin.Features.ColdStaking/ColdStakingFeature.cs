using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.ColdStaking.Controllers;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

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
    /// <para>Cold staking is a mechanism that eliminates the need to keep the coins in the hot wallet.
    /// With cold staking implemented, the miner still needs to be online and running a node with an open
    /// wallet, but the coins that are used for staking can be safely stored in cold storage. Therefore
    /// the open hot wallet does not need to hold any significant amount of coins, or it can even be
    /// completely empty.</para>
    /// </remarks>
    /// <seealso cref="ColdStakingManager.GetColdStakingScript(NBitcoin.ScriptId, NBitcoin.ScriptId)"/>
    /// <seealso cref="FullNodeFeature"/>
    public class ColdStakingFeature : FullNodeFeature
    {
        /// <summary>The logger factory used to create instance loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>The instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>The cold staking manager.</summary>
        private readonly ColdStakingManager coldStakingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColdStakingFeature"/> class.
        /// </summary>
        /// <param name="coldStakingManager">The cold staking manager.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="loggerFactory">The factory used to create instance loggers.</param>
        public ColdStakingFeature(
            ColdStakingManager coldStakingManager,
            IWalletManager walletManager,
            ILoggerFactory loggerFactory)
        {
            Guard.NotNull(coldStakingManager, nameof(coldStakingManager));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.coldStakingManager = coldStakingManager;
            this.walletManager = walletManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            this.logger.LogTrace("()");

            // The FullNodeFeature base class requires us to override this method for any feature-specific
            // initialization. If initialization is required it will be added here.

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is not a Stratis network.</exception>
    public static class FullNodeBuilderColdStakingExtension
    {
        public static IFullNodeBuilder UseColdStaking(this IFullNodeBuilder fullNodeBuilder)
        {
            // Ensure that this feature is only used on a Stratis network.
            if (!fullNodeBuilder.Network.Consensus.IsProofOfStake)
                throw new InvalidOperationException("Cold staking can only be used on a Stratis network.");

            // Register the cold staking script template.
            StandardScripts.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);

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
