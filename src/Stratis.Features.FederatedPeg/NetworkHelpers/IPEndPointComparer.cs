using System.Collections.Generic;
using System.Net;

namespace Stratis.Features.FederatedPeg.NetworkHelpers
{
    public class IPAddressComparer : IEqualityComparer<IPAddress>
    {
        public bool Equals(IPAddress x, IPAddress y)
        {
            if (x == null)
                return y == null;

            return x.MapToIPv6().Equals(y.MapToIPv6());
        }

        public int GetHashCode(IPAddress endPoint)
        {
            return endPoint.MapToIPv6().GetHashCode();
        }
    }
}
