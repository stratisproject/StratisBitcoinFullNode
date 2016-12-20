using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.RPC
{
    public static class WebHostExtensions
    {
		public static IWebHostBuilder ForFullNode(this IWebHostBuilder hostBuilder, FullNode fullNode)
		{
			hostBuilder.ConfigureServices(s =>
			{
				s.AddSingleton(fullNode);
			});
			return hostBuilder;
		}
	}
}
