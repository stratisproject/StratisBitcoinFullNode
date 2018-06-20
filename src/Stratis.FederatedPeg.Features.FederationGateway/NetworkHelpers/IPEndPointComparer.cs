using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using DBreeze.Utils;
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
}
