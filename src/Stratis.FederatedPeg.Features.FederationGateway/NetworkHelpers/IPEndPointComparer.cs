using System.Collections.Generic;
using System.Net;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers
{
    public class IPEndPointComparer : IEqualityComparer<IPEndPoint>
    {
        public bool Equals(IPEndPoint x, IPEndPoint y)
        {
            if (x == null) return y == null;

            return x.MapToIpv6().Equals(y.MapToIpv6());
        }

        public int GetHashCode(IPEndPoint endPoint)
        {
            return endPoint.MapToIpv6().GetHashCode();
        }
    }

    public class IPAddressComparer : IEqualityComparer<IPAddress>
    {
        public bool Equals(IPAddress x, IPAddress y)
        {
            if (x == null) return y == null;

            return x.MapToIPv6().Equals(y.MapToIPv6());
        }

        public int GetHashCode(IPAddress endPoint)
        {
            return endPoint.MapToIPv6().GetHashCode();
        }
    }
}
