using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder
{
    public class FullNodeBuilderTest
    {
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
            var settings = new NodeSettings();

            this.fullNodeBuilder = new FullNodeBuilder(settings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            Assert.Empty(this.featureCollection.FeatureRegistrations);
            Assert.Single(this.featureCollectionDelegates);
            Assert.Empty(this.serviceProviderDelegates);
            Assert.Single(this.serviceCollectionDelegates);
            Assert.Equal(Network.Main, this.fullNodeBuilder.Network);
            Assert.Equal(settings, this.fullNodeBuilder.NodeSettings);
        }

        [Fact]
        public void ConfigureServicesAddsServiceToDelegatesList()
        {
            var action = new Action<IServiceCollection>(e => { e.AddSingleton<IServiceProvider>(); });

            var result = this.fullNodeBuilder.ConfigureServices(action);

            Assert.Single(this.serviceCollectionDelegates);
            Assert.Equal(action, this.serviceCollectionDelegates[0]);
            Assert.Equal(this.fullNodeBuilder, result);
        }

        [Fact]
        public void ConfigureFeatureAddsFeatureToDelegatesList()
        {
            var action = new Action<IFeatureCollection>(e => { var registrations = e.FeatureRegistrations; });

            var result = this.fullNodeBuilder.ConfigureFeature(action);

            Assert.Single(this.featureCollectionDelegates);
            Assert.Equal(action, this.featureCollectionDelegates[0]);
            Assert.Equal(this.fullNodeBuilder, result);
        }

        [Fact]
        public void ConfigureServiceProviderAddsServiceProviderToDelegatesList()
        {
            var action = new Action<IServiceProvider>(e => { var serv = e.GetService(typeof(string)); });

            var result = this.fullNodeBuilder.ConfigureServiceProvider(action);

            Assert.Single(this.serviceProviderDelegates);
            Assert.Equal(action, this.serviceProviderDelegates[0]);
            Assert.Equal(this.fullNodeBuilder, result);
        }

        [Fact]
        public void BuildWithInitialServicesSetupConfiguresFullNodeUsingConfiguration()
        {
            var nodeSettings = new NodeSettings();
            nodeSettings.DataDir = "TestData/FullNodeBuilder/BuildWithInitialServicesSetup";
            nodeSettings.DataFolder = new DataFolder(nodeSettings);

            this.fullNodeBuilder = new FullNodeBuilder(nodeSettings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton<FullNode>();
                e.AddSingleton(nodeSettings.LoggerFactory);
            });

            this.fullNodeBuilder.ConfigureFeature(e =>
            {
                e.AddFeature<BlockStoreFeature>();
            });

            this.fullNodeBuilder.ConfigureServiceProvider(e =>
            {
                var settings = e.GetService<NodeSettings>();
                settings.Testnet = true;
            });

            var result = this.fullNodeBuilder.Build();

            Assert.NotNull(result);
        }

        [Fact]
        public void BuildConfiguresFullNodeUsingConfiguration()
        {
            var nodeSettings = new NodeSettings();
            nodeSettings.DataDir = "TestData/FullNodeBuilder/BuildConfiguresFullNodeUsingConfiguration";

            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton(nodeSettings);
                e.AddSingleton(nodeSettings.LoggerFactory);
                e.AddSingleton(nodeSettings.GetNetwork());
                e.AddSingleton<FullNode>();
            });

            this.fullNodeBuilder.ConfigureFeature(e =>
            {
                e.AddFeature<BlockStoreFeature>();
            });

            this.fullNodeBuilder.ConfigureServiceProvider(e =>
            {
                var settings = e.GetService<NodeSettings>();
                settings.Testnet = true;
            });

            var result = this.fullNodeBuilder.Build();

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
                    e.AddSingleton<Network>(NodeSettings.Default().GetNetwork());
                });

                this.fullNodeBuilder.Build();
                this.fullNodeBuilder.Build();
            });
        }

        [Fact]
        public void BuildTwiceThrowsException()
        {
            var nodeSettings = new NodeSettings();
            nodeSettings.DataDir = "TestData/FullNodeBuilder/BuildConfiguresFullNodeUsingConfiguration";

            Assert.Throws<InvalidOperationException>(() =>
            {
                this.fullNodeBuilder.ConfigureServices(e =>
                {
                    e.AddSingleton(nodeSettings);
                    e.AddSingleton(nodeSettings.LoggerFactory);
                    e.AddSingleton(nodeSettings.GetNetwork());
                    e.AddSingleton<FullNode>();
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

        [Fact]
        public void CanHaveAllServicesTest()
        {
            var nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = "Stratis.Bitcoin.Tests/TestData/FullNodeBuilderTest/CanHaveAllServicesTest";
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                // TODO: Re-factor by moving to Stratis.Bitcoin.Features.RPC.Tests or Stratis.Bitcoin.IntegrationTests
                //.AddRPC()
                .Build();

            IServiceProvider serviceProvider = fullNode.Services.ServiceProvider;
            var network = serviceProvider.GetService<Network>();
            var settings = serviceProvider.GetService<NodeSettings>();
            var consensusLoop = serviceProvider.GetService<ConsensusLoop>();
            var consensus = serviceProvider.GetService<PowConsensusValidator>();
            var chain = serviceProvider.GetService<NBitcoin.ConcurrentChain>();
            var chainState = serviceProvider.GetService<ChainState>();
            var blockStoreManager = serviceProvider.GetService<BlockStoreManager>();
            var mempoolManager = serviceProvider.GetService<MempoolManager>();
            var connectionManager = serviceProvider.GetService<ConnectionManager>();

            Assert.NotNull(fullNode);
            Assert.NotNull(network);
            Assert.NotNull(settings);
            Assert.NotNull(consensusLoop);
            Assert.NotNull(consensus);
            Assert.NotNull(chain);
            Assert.NotNull(chainState);
            Assert.NotNull(blockStoreManager);
            Assert.NotNull(mempoolManager);
        }

        [Fact]
        public void WhenNodeSettingsIsNullUseDefault()
        {
            var builder = new FullNodeBuilder(null);
            Assert.Equal(Network.Main, builder.Network);
        }
    }
}
