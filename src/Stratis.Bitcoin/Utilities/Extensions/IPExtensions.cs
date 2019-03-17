using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using TracerAttributes;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class IPExtensions
    {
        /// <summary>Maps an end point to IPv6 if is not already mapped.</summary>
        [NoTrace]
        public static IPEndPoint MapToIpv6(this IPEndPoint endPointv4)
        {
            if (endPointv4.Address.IsIPv4MappedToIPv6)
                return endPointv4;

            IPAddress mapped = new IPAddress(endPointv4.Address.GetAddressBytes()).MapToIPv6();
            var mappedIPEndPoint = new IPEndPoint(mapped, endPointv4.Port);
            return mappedIPEndPoint;
        }

        /// <summary>Match the end point with another by IP and port.</summary>
        [NoTrace]
        public static bool Match(this IPEndPoint endPoint, IPEndPoint matchWith)
        {
            return endPoint.MatchIpOnly(matchWith) && endPoint.Port == matchWith.Port;
        }

        /// <summary>Match the IP address only (the port is ignored).</summary>
        [NoTrace]
        public static bool MatchIpOnly(this IPEndPoint endPoint, IPEndPoint matchWith)
        {
            return endPoint.MapToIpv6().Address.ToString() == matchWith.MapToIpv6().Address.ToString();
        }

        /// <summary>
        /// Converts a string to an IP endpoint.
        /// </summary>
        /// <param name="ipAddress">String to convert.</param>
        /// <param name="port">Port to use if <paramref name="ipAddress"/> does not specify it.</param>
        /// <returns>IP end point representation of the string.</returns>
        /// <remarks>
        /// IP addresses can have a port specified such that the format of <paramref name="ipAddress"/> is as such: address:port.
        /// IPv4 and IPv6 addresses are supported.
        /// In the case where the default port is passed and the IP address has a port specified in it, the IP address's port will take precedence.
        /// Examples of addresses that are supported are:
        /// - 15.61.23.23
        /// - 15.61.23.23:1500
        /// - [1233:3432:2434:2343:3234:2345:6546:4534]
        /// - [1233:3432:2434:2343:3234:2345:6546:4534]:8333
        /// - ::ffff:192.168.4.1
        /// - ::ffff:192.168.4.1:80
        /// - google.com (Resolves domain name to IP Address)
        /// - google.com:80 ('')
        /// - 1233:3432:2434:2343:3234:2345:6546:4534
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown in case of the port number is out of range.</exception>
        /// <exception cref="FormatException">Thrown in case of ipAddress or port number is invalid.</exception>
        /// <exception cref="SocketException">Thrown if the ipAddress is not a valid host name.</exception>
        public static IPEndPoint ToIPEndPoint(this string ipAddress, int port)
        {
            return Utils.ParseIpEndpoint(ipAddress, port);
        }

        /// <summary>
        /// Determines if an endpoint includes another endpoint.
        /// </remarks>
        public static bool Contains(this IPEndPoint whiteBindEndpoint, IPEndPoint localEndpoint)
        {
            if (whiteBindEndpoint.Address.AnyIP())
                return whiteBindEndpoint.Port == localEndpoint.Port;

            return localEndpoint.Equals(whiteBindEndpoint);
        }

        public static bool AnyIP(this IPAddress address)
        {
            if (address.IsIPv4())
                return address.Equals(IPAddress.Parse("0.0.0.0"));

            return address.Equals(IPAddress.Parse("[::]"));
        }

        /// <summary>
        /// This method determines if any of a list of network end points can be mapped to this whitebind endpoint.
        /// </summary>
        public static bool CanBeMappedTo(this IPEndPoint whiteBindEndpoint, List<IPEndPoint> networkEndpoints, out IPEndPoint localEndpoint)
        {
            localEndpoint = networkEndpoints.SingleOrDefault(ep => whiteBindEndpoint.Contains(ep));

            return localEndpoint != null;
        }
    }
}