using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using NBitcoin;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNodeBuilder
	{
		NodeSettings NodeSettings { get; }

		Network Network { get; }

		IServiceCollection Services { get; }

		IFullNode Build();

		IFullNodeBuilder ConfigureFeature(Action<IFeatureCollection> configureFeatures);

		IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices);

		IFullNodeBuilder ConfigureServiceProvider(Action<IServiceProvider> configure);
	}
}