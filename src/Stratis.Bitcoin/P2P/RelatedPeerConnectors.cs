using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.P2P
{
    public sealed class RelatedPeerConnectors : Dictionary<string, PeerConnector>
    {
        public void Register(string name, PeerConnector connector)
        {
            if (connector != null)
            {
                this.Add(name, connector);
                connector.RelatedPeerConnector = this;
            }
        }

        public IPEndPoint[] GlobalConnectedNodes()
        {
            IPEndPoint[] all = new IPEndPoint[0];
            foreach (var kv in this)
            {
                var endPoints = kv.Value.ConnectedPeers.Select(n => n.RemoteSocketEndpoint).ToArray<IPEndPoint>();
                all = all.Union<IPEndPoint>(endPoints).ToArray<IPEndPoint>();
            }

            return all;
        }
    }
}
