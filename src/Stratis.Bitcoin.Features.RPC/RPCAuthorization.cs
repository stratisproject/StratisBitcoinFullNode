using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    public interface IRPCAuthorization
    {
        List<IPAddress> AllowIp { get; }

        List<string> Authorized { get; }

        bool IsAuthorized(string user);

        bool IsAuthorized(IPAddress ip);
    }

    public class RPCAuthorization : IRPCAuthorization
    {
        public RPCAuthorization()
        {
            this.AllowIp = new List<IPAddress>();
            this.Authorized = new List<string>();
        }

        public List<string> Authorized { get; }

        public List<IPAddress> AllowIp { get; }

        public bool IsAuthorized(string user)
        {
            Guard.NotEmpty(user, nameof(user));

            return this.Authorized.Any(a => a.Equals(user, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsAuthorized(IPAddress ip)
        {
            Guard.NotNull(ip, nameof(ip));

            if (this.AllowIp.Contains(IPAddress.IPv6Any))
                return true;

            if (ip.IsIPv4() && this.AllowIp.Contains(IPAddress.Any))
                return true;

            if (this.AllowIp.Count == 0)
                return true;
            return this.AllowIp.Any(i => i.AddressFamily == ip.AddressFamily && i.Equals(ip));
        }
    }
}
