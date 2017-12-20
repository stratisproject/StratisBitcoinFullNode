using System.Net;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class IPExtensions
    {
        /// <summary>Maps an end point to IPv6 if is not already mapped.</summary>
        public static IPEndPoint MapToIpv6(this IPEndPoint endPointv4)
        {
            if (endPointv4.Address.IsIPv4MappedToIPv6)
                return endPointv4;

            var mapped = new IPAddress(endPointv4.Address.GetAddressBytes()).MapToIPv6();
            var mappedIPEndPoint = new IPEndPoint(mapped, endPointv4.Port);
            return mappedIPEndPoint;
        }

        /// <summary>Match the end point with another by IP and port.</summary>
        public static bool Match(this IPEndPoint endPoint, IPEndPoint matchWith)
        {
            return endPoint.Address.ToString() == matchWith.MapToIpv6().Address.ToString() && endPoint.Port == matchWith.MapToIpv6().Port;
        }
    }
}