using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ProvenHeaders.Behaviors;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.Consensus.Tests")]

namespace Stratis.Bitcoin.Features.ProvenHeaders
{
    public class ProvenHeadersFeature : FullNodeFeature
    {
        private readonly Network network;
        private readonly IConnectionManager connectionManager;
        private readonly ILoggerFactory loggerFactory;
        private readonly ProvenHeadersConsensusManagerBehavior provenHeadersConsensusManagerBehavior;
        private readonly ProvenHeadersConnectionManagerBehavior provenHeadersConnectionManagerBehavior;
        private readonly ProvenHeadersBlockStoreBehavior provenHeadersBlockStoreBehavior;

        public ProvenHeadersFeature(
            Network network,
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            ProvenHeadersConsensusManagerBehavior provenHeadersConsensusManagerBehavior,
            ProvenHeadersConnectionManagerBehavior provenHeadersConnectionManagerBehavior,
            ProvenHeadersBlockStoreBehavior provenHeadersBlockStoreBehavior)
        {
            this.network = Guard.NotNull(network, nameof(network));
            this.connectionManager = Guard.NotNull(connectionManager, nameof(connectionManager));
            this.loggerFactory = Guard.NotNull(loggerFactory, nameof(loggerFactory));
            this.provenHeadersConsensusManagerBehavior = Guard.NotNull(provenHeadersConsensusManagerBehavior, nameof(provenHeadersConsensusManagerBehavior));
            this.provenHeadersConnectionManagerBehavior = Guard.NotNull(provenHeadersConnectionManagerBehavior, nameof(provenHeadersConnectionManagerBehavior));
            this.provenHeadersBlockStoreBehavior = Guard.NotNull(provenHeadersBlockStoreBehavior, nameof(provenHeadersBlockStoreBehavior));
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;

            // Replace CMB.
            bool oldCMBRemoved = connectionParameters.TemplateBehaviors.Remove(connectionParameters.TemplateBehaviors.Single(x => x is ConsensusManagerBehavior));
            Guard.Assert(oldCMBRemoved);
            connectionParameters.TemplateBehaviors.Add((ProvenHeadersConsensusManagerBehavior)this.provenHeadersConsensusManagerBehavior.Clone());

            // Replace connection manager behavior.
            bool oldConnectionManagerRemoved = connectionParameters.TemplateBehaviors.Remove(connectionParameters.TemplateBehaviors.Single(x => x is ConnectionManagerBehavior));
            Guard.Assert(oldConnectionManagerRemoved);
            connectionParameters.TemplateBehaviors.Add((ProvenHeadersConnectionManagerBehavior)this.provenHeadersConnectionManagerBehavior.Clone());

            // Replace block store.
            bool oldBlockStoreRemoved = connectionParameters.TemplateBehaviors.Remove(connectionParameters.TemplateBehaviors.Single(x => x is BlockStoreBehavior));
            Guard.Assert(oldBlockStoreRemoved);
            connectionParameters.TemplateBehaviors.Add((ProvenHeadersBlockStoreBehavior)this.provenHeadersBlockStoreBehavior.Clone());

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }
}