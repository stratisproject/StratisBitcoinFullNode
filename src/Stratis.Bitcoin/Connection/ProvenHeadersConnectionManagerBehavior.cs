using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Connection
{
    public class ProvenHeadersConnectionManagerBehavior : ConnectionManagerBehavior
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly ICheckpoints checkpoints;

        private readonly Network network;

        public ProvenHeadersConnectionManagerBehavior(IConnectionManager connectionManager, ILoggerFactory loggerFactory, ICheckpoints checkpoints, Network network)
            : base(connectionManager, loggerFactory)
        {
            this.checkpoints = checkpoints;
            this.network = network;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <inheritdoc />
        protected override async Task OnHandshakedAsync(INetworkPeer peer)
        {
            // Require from height is the highest between activation height and last checkpoint height.
            int lastCheckpointHeight = this.checkpoints.GetLastCheckpointHeight();
            int activationHeight = (this.network.Consensus.Options as PosConsensusOptions).ProvenHeadersActivationHeight;

            int requireFromHeight = Math.Max(lastCheckpointHeight, activationHeight);
            this.logger.LogDebug("Proven headers are requested from height {0}.", requireFromHeight);

            var sendProvenHeadersPayload = new SendProvenHeadersPayload(requireFromHeight);

            await peer.SendMessageAsync(sendProvenHeadersPayload).ConfigureAwait(false);
        }

        public override object Clone()
        {
            return new ProvenHeadersConnectionManagerBehavior(this.connectionManager, this.loggerFactory, this.checkpoints, this.network)
            {
                OneTry = this.OneTry,
                Whitelisted = this.Whitelisted,
            };
        }
    }
}
