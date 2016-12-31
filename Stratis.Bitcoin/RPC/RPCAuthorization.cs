using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC
{
    public class RPCAuthorization
    {

		private readonly List<string> _Authorized = new List<string>();
		public List<string> Authorized
		{
			get
			{
				return _Authorized;
			}
		}

		public List<IPAddress> AllowIp
		{
			get; set;
		} = new List<IPAddress>();

		public bool IsAuthorized(string user)
		{
			return Authorized.Any(a => a.Equals(user, StringComparison.OrdinalIgnoreCase));
		}
		public bool IsAuthorized(IPAddress ip)
		{
			if(AllowIp.Count == 0)
				return true;
			return AllowIp.Any(i => i.AddressFamily == ip.AddressFamily && i.Equals(ip));
		}
	}
}
