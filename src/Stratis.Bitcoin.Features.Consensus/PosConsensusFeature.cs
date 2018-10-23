using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Behaviors;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosConsensusFeature : ConsensusFeature
    {
        private readonly Network network;
        private readonly IChainState chainState;
        private readonly IConnectionManager connectionManager;
        private readonly IConsensusManager consensusManager;
        private readonly NodeDeployments nodeDeployments;
        private readonly ConcurrentChain chain;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;

        public PosConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments,
            ConcurrentChain chain,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            Signals.Signals signals,
            ILoggerFactory loggerFactory): base(network, chainState, connectionManager, signals, consensusManager, nodeDeployments)
        {
            this.network = network;
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

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            DeploymentFlags flags = this.nodeDeployments.GetFlags(this.consensusManager.Tip);
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;

            connectionParameters.TemplateBehaviors.Add(new ProvenHeadersConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory, this.network));

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }

    
}