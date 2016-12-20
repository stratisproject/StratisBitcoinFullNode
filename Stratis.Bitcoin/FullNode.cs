using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Stratis.Bitcoin.RPC;
using NBitcoin;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin
{
	public class FullNode
	{
		NodeArgs _Args;
		public FullNode(NodeArgs args)
		{
			if(args == null)
				throw new ArgumentNullException("args");
			_Args = args;
			Network = _Args.GetNetwork();
		}

		CancellationToken _Cancellation;

		public Network Network
		{
			get;
			internal set;
		}

		public void Start(CancellationToken cancellation = default(CancellationToken))
		{
			_Cancellation = cancellation;
			if(_Args.RPC != null)
			{
				var host = new WebHostBuilder()
				.UseKestrel()
				.ForFullNode(this)
				.UseUrls(_Args.RPC.GetUrls())
				.UseIISIntegration()
				.UseStartup<RPC.Startup>()
				.Build();
				host.Start();
				_Cancellation.Register(() => host.Dispose());
				Logs.RPC.LogInformation("RPC Server listening on: " + Environment.NewLine + String.Join(Environment.NewLine, _Args.RPC.GetUrls()));
			}
		}
	}
}
