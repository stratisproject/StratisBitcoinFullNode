using System;
using System.Collections.Generic;
using System.Linq;
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

		public bool IsAuthorized(string user)
		{
			return Authorized.Any(a => a.Equals(user, StringComparison.OrdinalIgnoreCase));
		}
	}
}
