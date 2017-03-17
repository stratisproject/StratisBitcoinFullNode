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

		IFullNodeBuilder ConfigureFeature(Action<FeatureCollection> configureFeatures);

		IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices);

		IFullNodeBuilder Configure(Action<IServiceProvider> configure);
	}
}