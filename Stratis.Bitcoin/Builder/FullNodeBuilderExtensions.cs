using System;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Builder
{
	public static class FullNodeBuilderExtensions
	{
		public static IFullNodeBuilder UseNodeSettings(this IFullNodeBuilder builder, NodeSettings nodeSettings)
		{
			var nodeBuilder = builder as FullNodeBuilder;
			nodeBuilder.NodeSettings = nodeSettings;
			nodeBuilder.Network = nodeSettings.GetNetwork();

			builder.ConfigureServices(service =>
			{
				service.AddSingleton(nodeBuilder.NodeSettings);
				service.AddSingleton(nodeBuilder.Network);
			});

            return builder.UseBaseFeature();
        }

		public static IFullNodeBuilder UseDefaultNodeSettings(this IFullNodeBuilder builder)
		{
			return builder.UseNodeSettings(NodeSettings.Default());
		}

		public static T Service<T>(this IServiceProvider serviceProvider)
		{
			return (T)serviceProvider.GetService<T>();
		}
	}
}