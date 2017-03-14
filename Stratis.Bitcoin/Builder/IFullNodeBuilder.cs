using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNodeBuilder
	{
		IServiceCollection Services { get; }

		IFullNode Build();

		IFullNodeBuilder ConfigureFeature(Action<FeatureCollection> configureFeatures);

		IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices);

		IFullNodeBuilder Configure(Action<IServiceProvider> configure);
	}
}