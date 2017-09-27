using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;

namespace Stratis.Bitcoin.Features.LightWallet
{
    /// <summary>
    /// Feature for a full-block SPV wallet.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
    public class LightWalletFeature : FullNodeFeature, INodeStats
    {
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IWalletManager walletManager;
        private readonly IConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly NodeDeployments nodeDeployments;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly INodeLifetime nodeLifetime;
        private readonly IWalletFeePolicy walletFeePolicy;
        private readonly BroadcasterBehavior broadcasterBehavior;

        /// <summary>
        /// Initializes a new instance of the <see cref="LightWalletFeature"/> class.
        /// </summary>
        /// <param name="walletSyncManager">The synchronization manager for the wallet, tasked with keeping the wallet synced with the network.</param>
        /// <param name="walletManager">The wallet manager.</param>
        /// <param name="connectionManager">The connection manager.</param>
        /// <param name="chain">The chain of blocks.</param>
        /// <param name="nodeDeployments">The node deployments.</param>
        /// <param name="asyncLoopFactory">The asynchronous loop factory.</param>
        /// <param name="nodeLifetime">The node lifetime.</param>
        /// <param name="walletFeePolicy">The wallet fee policy.</param>
        public LightWalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, IConnectionManager connectionManager,
            ConcurrentChain chain, NodeDeployments nodeDeployments, IAsyncLoopFactory asyncLoopFactory, INodeLifetime nodeLifetime, IWalletFeePolicy walletFeePolicy, BroadcasterBehavior broadcasterBehavior)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.connectionManager = connectionManager;
            this.chain = chain;
            this.nodeDeployments = nodeDeployments;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.walletFeePolicy = walletFeePolicy;
            this.broadcasterBehavior = broadcasterBehavior;
        }

        /// <inheritdoc />
        public override void Start()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(new DropNodesBehaviour(this.chain, this.connectionManager));

            this.walletManager.Initialize();
            this.walletSyncManager.Initialize();

            this.StartDeploymentsChecksLoop();

            this.walletFeePolicy.Initialize();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);
        }

        public void StartDeploymentsChecksLoop()
        {
            var loopToken = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping);
            this.asyncLoopFactory.Run("LightWalletFeature.CheckDeployments", token =>
            {
                if (!this.chain.IsDownloaded())
                    return Task.CompletedTask;

                // check segwit activation on the chain of headers
                // if segwit is active signal to only connect to 
                // nodes that also signal they are segwit nodes
                var flags = this.nodeDeployments.GetFlags(this.walletSyncManager.WalletTip);
                if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                    this.connectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);

                // done checking
                loopToken.Cancel();

                return Task.CompletedTask;
            },
                loopToken.Token,
                repeatEvery: TimeSpans.TenSeconds,
                startAfter: TimeSpans.TenSeconds);
        }

        /// <inheritdoc />
        public override void Stop()
        {
            base.Stop();
        }

        /// <inheritdoc />
        public void AddNodeStats(StringBuilder benchLog)
        {
            var manager = this.walletManager as WalletManager;

            if (manager != null)
            {
                var height = manager.LastBlockHeight();
                var block = this.chain.GetBlock(height);
                var hashBlock = block == null ? 0 : block.HashBlock;

                benchLog.AppendLine("LightWallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                     height.ToString().PadRight(8) +
                                     " LightWallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength + 3) +
                                     hashBlock);
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class LightWalletFeatureExtension
    {
        public static IFullNodeBuilder UseLightWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<LightWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IWalletSyncManager, LightWalletSyncManager>();
                        services.AddSingleton<IWalletTransactionHandler, WalletTransactionHandler>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<IWalletFeePolicy, LightWalletFeePolicy>();
                        services.AddSingleton<WalletController>();
                        services.AddSingleton<IBroadcasterManager, LightWalletBroadcasterManager>();
                        services.AddSingleton<BroadcasterBehavior>();

                    });
            });

            return fullNodeBuilder;
        }
    }
}
