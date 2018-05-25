using System;
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
        /// Examples of addresses that are supported are: 15.61.23.23, 15.61.23.23:1500, [1233:3432:2434:2343:3234:2345:6546:4534], [1233:3432:2434:2343:3234:2345:6546:4534]:8333.</remarks>
        /// <exception cref="ArgumentException">Thrown in case of the port number is out of range.</exception>    
        public static IPEndPoint ToIPEndPoint(this string ipAddress, int port)
        {
            // Checks the validity of the parameters passed.
            Guard.NotEmpty(ipAddress, nameof(ipAddress));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentException($"Port {port} was outside of the values that can assigned for a port [{IPEndPoint.MinPort}-{IPEndPoint.MaxPort}].");
            }

            int colon = ipAddress.LastIndexOf(':');

            // if a : is found, and it either follows a [...], or no other : is in the string, treat it as port separator
            bool fHaveColon = colon != -1;
            bool fBracketed = fHaveColon && (ipAddress[0] == '[' && ipAddress[colon - 1] == ']'); // if there is a colon, and in[0]=='[', colon is not 0, so in[colon-1] is safe
            bool fMultiColon = fHaveColon && (ipAddress.LastIndexOf(':', colon - 1) != -1);
            if (fHaveColon && (colon == 0 || fBracketed || !fMultiColon))
            {
                if (int.TryParse(ipAddress.Substring(colon + 1), out var n) && n > IPEndPoint.MinPort && n < IPEndPoint.MaxPort)
                {
                    ipAddress = ipAddress.Substring(0, colon);
                    port = n;
                }
            }

            return new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }
    }
}