using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Stratis.Bitcoin.RPC;

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
		}

		CancellationToken _Cancellation;
		public void Start(CancellationToken cancellation = default(CancellationToken))
		{
			_Cancellation = cancellation;
			if(_Args.RPC != null)
			{
				var host = new WebHostBuilder()
				.UseKestrel()
				.ForFullNode(this)
				.UseContentRoot(_Args.DataDir)
				.UseIISIntegration()
				.UseStartup<RPC.Startup>()
				.Build();
				host.Start();
				_Cancellation.Register(() => host.Dispose());
			}	
		}
    }
}
