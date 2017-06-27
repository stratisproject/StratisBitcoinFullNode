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

	public class FullNodeBuilder : IFullNodeBuilder
	{
		private readonly List<Action<IServiceProvider>> configureDelegates;
		private readonly List<Action<IServiceCollection>> configureServicesDelegates;
		private readonly List<Action<IFeatureCollection>> featuresRegistrationDelegates;

		private bool fullNodeBuilt;

		public FullNodeBuilder() :
			this(new List<Action<IServiceCollection>>(),
				new List<Action<IServiceProvider>>(),
				new List<Action<IFeatureCollection>>(),
				new FeatureCollection())
		{
		}

		/// <summary>
		/// Accepts a NodeSettings instance and register required services
		/// </summary>
		/// <param name="nodeSettings">The node settings.</param>
		public FullNodeBuilder(NodeSettings nodeSettings)
			: this(nodeSettings, new List<Action<IServiceCollection>>(),
				new List<Action<IServiceProvider>>(),
				new List<Action<IFeatureCollection>>(),
				new FeatureCollection())
		{

		}

		internal FullNodeBuilder(NodeSettings nodeSettings, List<Action<IServiceCollection>> configureServicesDelegates, List<Action<IServiceProvider>> configureDelegates,
			List<Action<IFeatureCollection>> featuresRegistrationDelegates, IFeatureCollection features)
			: this(configureServicesDelegates, configureDelegates, featuresRegistrationDelegates, features)
		{
            this.NodeSettings = nodeSettings ?? NodeSettings.Default();
            this.Network = this.NodeSettings.GetNetwork();

			this.ConfigureServices(service =>
			{
				service.AddSingleton(this.NodeSettings);
				service.AddSingleton(this.Network);
			});

			this.UseBaseFeature();
		}

		internal FullNodeBuilder(List<Action<IServiceCollection>> configureServicesDelegates, List<Action<IServiceProvider>> configureDelegates, 
			List<Action<IFeatureCollection>> featuresRegistrationDelegates, IFeatureCollection features)
		{
			Guard.NotNull(configureServicesDelegates, nameof(configureServicesDelegates));
			Guard.NotNull(configureDelegates, nameof(configureDelegates));
			Guard.NotNull(featuresRegistrationDelegates, nameof(featuresRegistrationDelegates));
			Guard.NotNull(features, nameof(features));

			this.configureServicesDelegates = configureServicesDelegates;
			this.configureDelegates = configureDelegates;
			this.featuresRegistrationDelegates = featuresRegistrationDelegates;
			this.Features = features;
		}				

		public IFeatureCollection Features { get; }

		public NodeSettings NodeSettings { get; set; }

		public Network Network { get; set; }

		public IServiceCollection Services { get; private set; }

		/// <summary>
		/// Adds services to the builder. 
		/// </summary>
		/// <param name="configureServices">A method that adds services to the builder</param>
		/// <returns>An IFullNodebuilder</returns>
		public IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices)
		{
			Guard.NotNull(configureServices, nameof(configureServices));

			this.configureServicesDelegates.Add(configureServices);
			return this;
		}

		/// <summary>
		/// Adds features to the builder. 
		/// </summary>
		/// <param name="configureFeatures">A method that adds features to the collection</param>
		/// <returns>An IFullNodebuilder</returns>
		public IFullNodeBuilder ConfigureFeature(Action<IFeatureCollection> configureFeatures)
		{
			Guard.NotNull(configureFeatures, nameof(configureFeatures));

			this.featuresRegistrationDelegates.Add(configureFeatures);
			return this;
		}

		/// <summary>
		/// Add configurations for the service provider.
		/// </summary>
		/// <param name="configure">A method that configures the service provider.</param>
		/// <returns>An IFullNodebuilder</returns>
		public IFullNodeBuilder ConfigureServiceProvider(Action<IServiceProvider> configure)
		{
			Guard.NotNull(configure, nameof(configure));

			this.configureDelegates.Add(configure);
			return this;
		}

		public IFullNode Build()
		{
			if (this.fullNodeBuilt)
				throw new InvalidOperationException("full node already built");
			this.fullNodeBuilt = true;

			this.Services = this.BuildServices();

			var fullNodeServiceProvider = this.Services.BuildServiceProvider();
			this.ConfigureServices(fullNodeServiceProvider);

			//obtain the nodeSettings from the service (it's set used FullNodeBuilder.UseNodeSettings)
			var nodeSettings = fullNodeServiceProvider.GetService<NodeSettings>();
			if (nodeSettings == null)
				throw new NodeBuilderException("NodeSettings not specified");

			var network = fullNodeServiceProvider.GetService<Network>();
			if (network == null)
				throw new NodeBuilderException("Network not specified");

			var fullNode = fullNodeServiceProvider.GetService<FullNode>();
			if (fullNode == null)
				throw new InvalidOperationException("Fullnode not registered with provider");

			fullNode.Initialize(new FullNodeServiceProvider(
				fullNodeServiceProvider,
				this.Features.FeatureRegistrations.Select(s => s.FeatureType).ToList()));

			return fullNode;
		}

		private IServiceCollection BuildServices()
		{
			this.Services = new ServiceCollection();

			// register services before features 
			// as some of the features may depend on independent services
			foreach (var configureServices in this.configureServicesDelegates)
				configureServices(this.Services);

			// configure features
			foreach (var configureFeature in this.featuresRegistrationDelegates)
				configureFeature(this.Features);

			// configure features startup
			foreach (var featureRegistration in this.Features.FeatureRegistrations)
				featureRegistration.BuildFeature(this.Services);

			return this.Services;
		}

		private void ConfigureServices(IServiceProvider serviceProvider)
		{
			// configure registered services
			foreach (var configure in this.configureDelegates)
				configure(serviceProvider);
		}
	}
}