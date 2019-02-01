using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusFeature : FullNodeFeature
    {
        private readonly IChainState chainState;

        private readonly IConnectionManager connectionManager;

        private readonly Signals.ISignals signals;

        private readonly IConsensusManager consensusManager;

        private readonly NodeDeployments nodeDeployments;

        public ConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            Signals.ISignals signals,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments)
        {
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.signals = signals;
            this.consensusManager = consensusManager;
            this.nodeDeployments = nodeDeployments;

            this.chainState.MaxReorgLength = network.Consensus.MaxReorgLength;
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