using System;
using System.Collections.Generic;
using System.Net;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Features.FederatedPeg.NetworkHelpers
{
    public class IPEndPointComparer : IComparer<IPEndPoint>
    {
        public bool Equals(IPEndPoint x, IPEndPoint y)
        {
            return this.Compare(x, y) == 0;
        }

        public int Compare(IPEndPoint x, IPEndPoint y)
        {
            if (x == null || y == null)
            {
                if (x == null && y == null)
                    return 0;

                return (x == null) ? -1 : 1;
            }

            byte[] addr1 = x.MapToIpv6().Address.GetAddressBytes();
            byte[] addr2 = y.MapToIpv6().Address.GetAddressBytes();

            for (int i = 0; i < addr1.Length; i++)
                if (addr1[i] != addr2[i])
                    return (addr1[i] < addr2[i]) ? -1 : 1;

            return x.Port.CompareTo(y.Port);
        }

        public int GetHashCode(IPEndPoint endPoint)
        {
            return endPoint.Address.MapToIPv6().GetHashCode() ^ endPoint.Port;
        }
    }
}
