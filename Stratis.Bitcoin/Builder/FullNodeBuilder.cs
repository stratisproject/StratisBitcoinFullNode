using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder
{
	public class NodeBuilderException : Exception
	{
		public NodeBuilderException(string message) : base(message)
		{
		}
	}

	public class FullNodeServiceProvider
	{
		private readonly List<Type> featureTypes;

		public FullNodeServiceProvider(IServiceProvider serviceProvider, List<Type> featureTypes)
		{
			ServiceProvider = serviceProvider;
			this.featureTypes = featureTypes;
		}

		public IServiceProvider ServiceProvider { get; }

		public IEnumerable<IFullNodeFeature> Features
		{
			get
			{
				// features are enumerated in the same order 
				// they where registered with the builder

				foreach (var featureDescriptor in featureTypes)
					yield return ServiceProvider.GetService(featureDescriptor) as IFullNodeFeature;
			}
		}
	}

	public class FullNodeBuilder : IFullNodeBuilder
	{
		private readonly List<Action<IServiceProvider>> configureDelegates;
		private readonly List<Action<IServiceCollection>> configureServicesDelegates;
		private readonly List<Action<FeatureCollection>> featuresRegistrationDelegates;

		private bool fullNodeBuilt;

		public FullNodeBuilder()
		{
			configureServicesDelegates = new List<Action<IServiceCollection>>();
			configureDelegates = new List<Action<IServiceProvider>>();
			featuresRegistrationDelegates = new List<Action<FeatureCollection>>();
			Features = new FeatureCollection();			
		}

		public FeatureCollection Features { get; }

		public NodeArgs NodeArgs { get; set; }
		public Network Network { get; set; }

		public IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices)
		{
			Guard.NotNull(configureServices, nameof(configureServices));

			configureServicesDelegates.Add(configureServices);
			return this;
		}

		public IFullNodeBuilder ConfigureFeature(Action<FeatureCollection> configureFeatures)
		{
			Guard.NotNull(configureFeatures, nameof(configureFeatures));

			featuresRegistrationDelegates.Add(configureFeatures);
			return this;
		}


		public IFullNodeBuilder Configure(Action<IServiceProvider> configure)
		{
			if (configure == null)
				throw new ArgumentNullException(nameof(configure));

			configureDelegates.Add(configure);
			return this;
		}

		public IServiceCollection Services { get; private set; }

		public IFullNode Build()
		{
			if (fullNodeBuilt)
				throw new InvalidOperationException("full node already built");
			fullNodeBuilt = true;

			Services = BuildServices();

			var fullNodeServiceProvider = Services.BuildServiceProvider();
			ConfigureServices(fullNodeServiceProvider);

			//obtain the nodeArgs from the service (it's set used FullNodeBuilder.UseNodeArgs)
			var nodeArgs = fullNodeServiceProvider.GetService<NodeArgs>();
			if (nodeArgs == null)
				throw new NodeBuilderException("NodeArgs not specified");

			var fullNode = fullNodeServiceProvider.GetService<FullNode>();


			fullNode.Initialize(new FullNodeServiceProvider(
				fullNodeServiceProvider,
				Features.FeatureRegistrations.Select(s => s.FeatureType).ToList()));

			return fullNode;
		}

		private IServiceCollection BuildServices()
		{
			Services = new ServiceCollection();

			// register services before features 
			// as some of the features may depend on independent services
			foreach (var configureServices in configureServicesDelegates)
				configureServices(Services);

			// configure features
			foreach (var configureFeature in featuresRegistrationDelegates)
				configureFeature(Features);

			// configure features startup
			foreach (var featureRegistration in Features.FeatureRegistrations)
				featureRegistration.BuildFeature(Services);

			return Services;
		}

		private void ConfigureServices(IServiceProvider serviceProvider)
		{
			// configure registered services
			foreach (var configure in configureDelegates)
				configure(serviceProvider);
		}
	}
}