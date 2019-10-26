using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder
{
    /// <summary>
    /// Exception thrown by FullNodeBuilder.Build.
    /// </summary>
    /// <seealso cref="FullNodeBuilder.Build"/>
    public class NodeBuilderException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public NodeBuilderException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Full node builder allows constructing a full node using specific components.
    /// </summary>
    public class FullNodeBuilder : IFullNodeBuilder
    {
        /// <summary>List of delegates that configure the service providers.</summary>
        private readonly List<Action<IServiceProvider>> configureDelegates;

        /// <summary>List of delegates that add services to the builder.</summary>
        private readonly List<Action<IServiceCollection>> configureServicesDelegates;

        /// <summary>List of delegates that add features to the collection.</summary>
        private readonly List<Action<IFeatureCollection>> featuresRegistrationDelegates;

        /// <summary>true if the Build method has been called already (whether it succeeded or not), false otherwise.</summary>
        private bool fullNodeBuilt;

        /// <summary>Collection of features available to and/or used by the node.</summary>
        public IFeatureCollection Features { get; private set; }

        /// <inheritdoc />
        public NodeSettings NodeSettings { get; set; }

        /// <inheritdoc />
        public Network Network { get; set; }

        /// <summary>Collection of DI services.</summary>
        public IServiceCollection Services { get; private set; }

        /// <summary>
        /// Initializes a default instance of the object and registers required services.
        /// </summary>
        public FullNodeBuilder() :
            this(new List<Action<IServiceCollection>>(),
                new List<Action<IServiceProvider>>(),
                new List<Action<IFeatureCollection>>(),
                new FeatureCollection())
        {
        }

        /// <summary>
        /// Initializes an instance of the object using specific NodeSettings instance and registers required services.
        /// </summary>
        /// <param name="nodeSettings">User defined node settings.</param>
        public FullNodeBuilder(NodeSettings nodeSettings)
            : this(nodeSettings, new List<Action<IServiceCollection>>(),
                new List<Action<IServiceProvider>>(),
                new List<Action<IFeatureCollection>>(),
                new FeatureCollection())
        {
        }

        /// <summary>
        /// Initializes an instance of the object using specific NodeSettings instance and configuration delegates and registers required services.
        /// </summary>
        /// <param name="nodeSettings">User defined node settings.</param>
        /// <param name="configureServicesDelegates">List of delegates that add services to the builder.</param>
        /// <param name="configureDelegates">List of delegates that configure the service providers.</param>
        /// <param name="featuresRegistrationDelegates">List of delegates that add features to the collection.</param>
        /// <param name="features">Collection of features to be available to and/or used by the node.</param>
        internal FullNodeBuilder(NodeSettings nodeSettings, List<Action<IServiceCollection>> configureServicesDelegates, List<Action<IServiceProvider>> configureDelegates,
            List<Action<IFeatureCollection>> featuresRegistrationDelegates, IFeatureCollection features)
            : this(configureServicesDelegates, configureDelegates, featuresRegistrationDelegates, features)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.NodeSettings = nodeSettings;
            this.Network = this.NodeSettings.Network;

            this.ConfigureServices(service =>
            {
                service.AddSingleton(this.NodeSettings);
                service.AddSingleton(this.Network);
            });

            this.UseBaseFeature();
        }

        /// <summary>
        /// Initializes an instance of the object using specific configuration delegates.
        /// </summary>
        /// <param name="configureServicesDelegates">List of delegates that add services to the builder.</param>
        /// <param name="configureDelegates">List of delegates that configure the service providers.</param>
        /// <param name="featuresRegistrationDelegates">List of delegates that add features to the collection.</param>
        /// <param name="features">Collection of features to be available to and/or used by the node.</param>
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

        /// <inheritdoc />
        public IFullNodeBuilder ConfigureFeature(Action<IFeatureCollection> configureFeatures)
        {
            Guard.NotNull(configureFeatures, nameof(configureFeatures));

            this.featuresRegistrationDelegates.Add(configureFeatures);
            return this;
        }

        /// <inheritdoc />
        public IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices)
        {
            Guard.NotNull(configureServices, nameof(configureServices));

            this.configureServicesDelegates.Add(configureServices);
            return this;
        }

        /// <inheritdoc />
        public IFullNodeBuilder ConfigureServiceProvider(Action<IServiceProvider> configure)
        {
            Guard.NotNull(configure, nameof(configure));

            this.configureDelegates.Add(configure);
            return this;
        }

        /// <inheritdoc />
        public IFullNode Build()
        {
            if (this.fullNodeBuilt)
                throw new InvalidOperationException("full node already built");
            this.fullNodeBuilt = true;

            this.Services = this.BuildServices();

            // Print command-line help
            if (this.NodeSettings?.PrintHelpAndExit ?? false)
            {
                NodeSettings.PrintHelp(this.Network);

                foreach (IFeatureRegistration featureRegistration in this.Features.FeatureRegistrations)
                {
                    MethodInfo printHelp = featureRegistration.FeatureType.GetMethod("PrintHelp", BindingFlags.Public | BindingFlags.Static);

                    printHelp?.Invoke(null, new object[] { this.NodeSettings.Network });
                }

                // Signal node not built
                return null;
            }

            // Create configuration file if required
            this.NodeSettings?.CreateDefaultConfigurationFile(this.Features.FeatureRegistrations);

            ServiceProvider fullNodeServiceProvider = this.Services.BuildServiceProvider();
            this.ConfigureServices(fullNodeServiceProvider);

            // Obtain the nodeSettings from the service (it's set used FullNodeBuilder.UseNodeSettings)
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

        /// <summary>
        /// Constructs and configures services ands features to be used by the node.
        /// </summary>
        /// <returns>Collection of registered services.</returns>
        private IServiceCollection BuildServices()
        {
            this.Services = new ServiceCollection();

            // register services before features
            // as some of the features may depend on independent services
            foreach (Action<IServiceCollection> configureServices in this.configureServicesDelegates)
                configureServices(this.Services);

            // configure features
            foreach (Action<IFeatureCollection> configureFeature in this.featuresRegistrationDelegates)
                configureFeature(this.Features);

            // configure features startup
            foreach (IFeatureRegistration featureRegistration in this.Features.FeatureRegistrations)
            {
                try
                {
                    featureRegistration.EnsureDependencies(this.Features.FeatureRegistrations);
                }
                catch (MissingDependencyException e)
                {
                    this.NodeSettings.Logger.LogCritical("Feature {0} cannot be configured because it depends on other features that were not registered",
                        featureRegistration.FeatureType.Name);

                    throw e;
                }

                featureRegistration.BuildFeature(this.Services);
            }

            return this.Services;
        }

        /// <summary>
        /// Configure registered services.
        /// </summary>
        /// <param name="serviceProvider"></param>
        private void ConfigureServices(IServiceProvider serviceProvider)
        {
            foreach (Action<IServiceProvider> configure in this.configureDelegates)
                configure(serviceProvider);
        }
    }
}