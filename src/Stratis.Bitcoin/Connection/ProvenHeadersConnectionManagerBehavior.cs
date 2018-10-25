using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Connection
{
    public class ProvenHeadersConnectionManagerBehavior : ConnectionManagerBehavior
    {
        public ProvenHeadersConnectionManagerBehavior(IConnectionManager connectionManager, ILoggerFactory loggerFactory)
            : base(connectionManager, loggerFactory)
        {
        }

        /// <inheritdoc />
        protected override async Task OnHandshakedAsync(INetworkPeer peer)
        {
            var sendProvenHeadersPayload = new SendProvenHeadersPayload();

            // TODO require height

            await peer.SendMessageAsync(sendProvenHeadersPayload).ConfigureAwait(false);
        }

        public override object Clone()
        {
            return new ProvenHeadersConnectionManagerBehavior(this.connectionManager, this.loggerFactory)
            {
                OneTry = this.OneTry,
                Whitelisted = this.Whitelisted,
            };
        }
    }
}
