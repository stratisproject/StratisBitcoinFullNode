using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Configuration;

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