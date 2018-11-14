using System;
using System.Threading;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class CurrentPeerContext
    {
        private static readonly AsyncLocal<INetworkPeer> CallContext = new AsyncLocal<INetworkPeer>();

        public static IDisposable Capture(INetworkPeer principal)
        {
            INetworkPeer current = Get();

            Set(principal);

            return new AmbientContextActionDisposable(() => Set(current));
        }

        public static void Set(INetworkPeer value)
        {
            CallContext.Value = value;
        }

        public static INetworkPeer Get()
        {
            return CallContext.Value;
        }

        public static void Clear()
        {
            CallContext.Value = null;
        }
    }
}
