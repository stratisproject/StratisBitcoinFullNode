using System;
using System.Net;
using NBitcoin;

namespace Stratis.Bitcoin.P2P
{
    public sealed class WellKnownPeerConnectorSelectors
    {
        private static Func<IPEndPoint, byte[]> byEndpoint;

        public static Func<IPEndPoint, byte[]> ByEndpoint
        {
            get
            {
                return byEndpoint = byEndpoint ?? new Func<IPEndPoint, byte[]>((endpoint) =>
                {
                    var bytes = endpoint.Address.GetAddressBytes();
                    var port = Utils.ToBytes((uint)endpoint.Port, true);
                    var result = new byte[bytes.Length + port.Length];
                    Array.Copy(bytes, result, bytes.Length);
                    Array.Copy(port, 0, result, bytes.Length, port.Length);
                    return bytes;
                });
            }
        }

        private static Func<IPEndPoint, byte[]> byNetwork;

        public static Func<IPEndPoint, byte[]> ByNetwork
        {
            get
            {
                return byNetwork = byNetwork ?? new Func<IPEndPoint, byte[]>((ip) =>
                {
                    return IpExtensions.GetGroup(ip.Address);
                });
            }
        }
    }
}
