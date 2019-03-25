using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PowConsensusFeature : ConsensusFeature
    {
        private readonly IChainState chainState;
        private readonly IConnectionManager connectionManager;
        private readonly IConsensusManager consensusManager;
        private readonly NodeDeployments nodeDeployments;
        private readonly ConcurrentChain chain;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;

        public PowConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments,
            ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            Signals.ISignals signals,
            ILoggerFactory loggerFactory) : base(network, chainState, connectionManager, signals, consensusManager, nodeDeployments)
        {
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.consensusManager = consensusManager;
            this.nodeDeployments = nodeDeployments;
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;

            this.chainState.MaxReorgLength = network.Consensus.MaxReorgLength;
        }

        /// <summary>
        /// Prints command-line help. Invoked via reflection.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static new void PrintHelp(Network network)
        {
            ConsensusFeature.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration. Invoked via reflection.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static new void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ConsensusFeature.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            DeploymentFlags flags = this.nodeDeployments.GetFlags(this.consensusManager.Tip);
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }


}