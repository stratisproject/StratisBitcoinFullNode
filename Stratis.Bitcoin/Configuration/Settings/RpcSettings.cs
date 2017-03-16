using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.Configuration.Settings
{
	public class RpcSettings
	{
		public RpcSettings()
		{
			Bind = new List<IPEndPoint>();
			AllowIp = new List<IPAddress>();
		}

		public string RpcUser
		{
			get; set;
		}
		public string RpcPassword
		{
			get; set;
		}

		public int RPCPort
		{
			get; set;
		}
		public List<IPEndPoint> Bind
		{
			get; set;
		}

		public List<IPAddress> AllowIp
		{
			get; set;
		}

		public string[] GetUrls()
		{
			return Bind.Select(b => "http://" + b + "/").ToArray();
		}
	}
}