using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder
{
    public class FullNodeBuilderTest
    {
        public class DummyFeature : FullNodeFeature
        {
            public override Task InitializeAsync()
            {
                // nothing.
                return Task.CompletedTask;
            }
        }

        private FeatureCollection featureCollection;
        private List<Action<IFeatureCollection>> featureCollectionDelegates;
        private FullNodeBuilder fullNodeBuilder;
        private List<Action<IServiceCollection>> serviceCollectionDelegates;
        private List<Action<IServiceProvider>> serviceProviderDelegates;

        public FullNodeBuilderTest()
        {
            this.serviceCollectionDelegates = new List<Action<IServiceCollection>>();
            this.serviceProviderDelegates = new List<Action<IServiceProvider>>();
            this.featureCollectionDelegates = new List<Action<IFeatureCollection>>();
            this.featureCollection = new FeatureCollection();

            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            this.fullNodeBuilder.Network = KnownNetworks.RegTest;
        }

        [Fact]
        public void ConstructorWithoutNodeSettingsDoesNotSetupBaseServices()
        {
            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            Assert.Empty(this.featureCollection.FeatureRegistrations);
            Assert.Empty(this.featureCollectionDelegates);
            Assert.Empty(this.serviceProviderDelegates);
            Assert.Empty(this.serviceCollectionDelegates);
            Assert.Null(this.fullNodeBuilder.Network);
            Assert.Null(this.fullNodeBuilder.NodeSettings);
        }

        [Fact]
        public void ConstructorWithNodeSettingsSetupBaseServices()
        {
            var settings = new NodeSettings(KnownNetworks.RegTest);

            this.fullNodeBuilder = new FullNodeBuilder(settings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            Assert.Empty(this.featureCollection.FeatureRegistrations);
            Assert.Single(this.featureCollectionDelegates);
            Assert.Empty(this.serviceProviderDelegates);
            Assert.Single(this.serviceCollectionDelegates);
            Assert.Equal(KnownNetworks.RegTest, this.fullNodeBuilder.Network);
            Assert.Equal(settings, this.fullNodeBuilder.NodeSettings);
        }

        [Fact]
        public void ConfigureServicesAddsServiceToDelegatesList()
        {
            var action = new Action<IServiceCollection>(e => { e.AddSingleton<IServiceProvider>(); });

            IFullNodeBuilder result = this.fullNodeBuilder.ConfigureServices(action);

            Assert.Single(this.serviceCollectionDelegates);
            Assert.Equal(action, this.serviceCollectionDelegates[0]);
            Assert.Equal(this.fullNodeBuilder, result);
        }

        [Fact]
        public void ConfigureFeatureAddsFeatureToDelegatesList()
        {
            var action = new Action<IFeatureCollection>(e => { List<IFeatureRegistration> registrations = e.FeatureRegistrations; });

            IFullNodeBuilder result = this.fullNodeBuilder.ConfigureFeature(action);

            Assert.Single(this.featureCollectionDelegates);
            Assert.Equal(action, this.featureCollectionDelegates[0]);
            Assert.Equal(this.fullNodeBuilder, result);
        }

        [Fact]
        public void ConfigureServiceProviderAddsServiceProviderToDelegatesList()
        {
            var action = new Action<IServiceProvider>(e => { object serv = e.GetService(typeof(string)); });

            IFullNodeBuilder result = this.fullNodeBuilder.ConfigureServiceProvider(action);

            Assert.Single(this.serviceProviderDelegates);
            Assert.Equal(action, this.serviceProviderDelegates[0]);
            Assert.Equal(this.fullNodeBuilder, result);
        }

        [Fact]
        public void BuildWithInitialServicesSetupConfiguresFullNodeUsingConfiguration()
        {
            string dataDir = "TestData/FullNodeBuilder/BuildWithInitialServicesSetup";
            var nodeSettings = new NodeSettings(KnownNetworks.StratisRegTest, args: new string[] { $"-datadir={dataDir}" });

            this.fullNodeBuilder = new FullNodeBuilder(nodeSettings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton<FullNode>();
                e.AddSingleton(nodeSettings.LoggerFactory);
            });

            this.fullNodeBuilder.ConfigureFeature(e =>
            {
                e.AddFeature<DummyFeature>();
            });

            IFullNode result = this.fullNodeBuilder.UsePosConsensus().Build();

            Assert.NotNull(result);
        }

        [Fact]
        public void BuildConfiguresFullNodeUsingConfiguration()
        {
            string dataDir = "TestData/FullNodeBuilder/BuildConfiguresFullNodeUsingConfiguration";
            var nodeSettings = new NodeSettings(KnownNetworks.StratisRegTest, args: new string[] { $"-datadir={dataDir}" });

            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton(nodeSettings);
                e.AddSingleton(nodeSettings.LoggerFactory);
                e.AddSingleton(nodeSettings.Network);
                e.AddSingleton<FullNode>();
                e.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            });

            this.fullNodeBuilder.ConfigureFeature(e =>
            {
                e.AddFeature<DummyFeature>();
            });

            IFullNode result = this.fullNodeBuilder.Build();

            Assert.NotNull(result);
        }

        [Fact]
        public void BuildWithoutFullNodeInServiceConfigurationThrowsException()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                this.fullNodeBuilder.ConfigureServices(e =>
                {
                    e.AddSingleton<NodeSettings>();
                    e.AddSingleton<Network>(NodeSettings.Default(this.fullNodeBuilder.Network).Network);
                });

                this.fullNodeBuilder.Build();
                this.fullNodeBuilder.Build();
            });
        }

        [Fact]
        public void BuildTwiceThrowsException()
        {
            string dataDir = "TestData/FullNodeBuilder/BuildConfiguresFullNodeUsingConfiguration";
            var nodeSettings = new NodeSettings(KnownNetworks.StratisRegTest, args: new string[] { $"-datadir={dataDir}" });

            Assert.Throws<InvalidOperationException>(() =>
            {
                this.fullNodeBuilder.ConfigureServices(e =>
                {
                    e.AddSingleton(nodeSettings);
                    e.AddSingleton(nodeSettings.LoggerFactory);
                    e.AddSingleton(nodeSettings.Network);
                    e.AddSingleton<FullNode>();
                    e.AddSingleton<IDateTimeProvider, DateTimeProvider>();
                });

                this.fullNodeBuilder.Build();
                this.fullNodeBuilder.Build();
            });
        }

        [Fact]
        public void BuildWithoutNodeSettingsInServiceConfigurationThrowsException()
        {
            Assert.Throws<NodeBuilderException>(() =>
            {
                this.fullNodeBuilder.Build();
            });
        }
    }
}
