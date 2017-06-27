using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.Configuration.Settings
{
	public class RpcSettings
	{
		public RpcSettings()
		{
            this.Bind = new List<IPEndPoint>();
            this.AllowIp = new List<IPAddress>();
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
			return this.Bind.Select(b => "http://" + b + "/").ToArray();
		}
	}
}