using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.Builder
{
    [TestClass]
    public class FullNodeBuilderTest
    {
        private FeatureCollection featureCollection;
        private List<Action<IFeatureCollection>> featureCollectionDelegates;
        private FullNodeBuilder fullNodeBuilder;
        private List<Action<IServiceCollection>> serviceCollectionDelegates;
        private List<Action<IServiceProvider>> serviceProviderDelegates;

        [TestInitialize]
        public void Initialize()
        {
            this.serviceCollectionDelegates = new List<Action<IServiceCollection>>();
            this.serviceProviderDelegates = new List<Action<IServiceProvider>>();
            this.featureCollectionDelegates = new List<Action<IFeatureCollection>>();
            this.featureCollection = new FeatureCollection();

            Logs.Configure(new LoggerFactory());

            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
        }

        [TestMethod]
        public void ConstructorWithoutNodeSettingsDoesNotSetupBaseServices()
        {
            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            Assert.AreEqual(0, this.featureCollection.FeatureRegistrations.Count);
            Assert.AreEqual(0, this.featureCollectionDelegates.Count);
            Assert.AreEqual(0, this.serviceProviderDelegates.Count);
            Assert.AreEqual(0, this.serviceCollectionDelegates.Count);
            Assert.AreEqual(null, this.fullNodeBuilder.Network);
            Assert.AreEqual(null, this.fullNodeBuilder.NodeSettings);
        }

        [TestMethod]
        public void ConstructorWithNodeSettingsSetupBaseServices()
        {
            var settings = new NodeSettings();

            this.fullNodeBuilder = new FullNodeBuilder(settings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            Assert.AreEqual(0, this.featureCollection.FeatureRegistrations.Count);
            Assert.AreEqual(1, this.featureCollectionDelegates.Count);
            Assert.AreEqual(0, this.serviceProviderDelegates.Count);
            Assert.AreEqual(1, this.serviceCollectionDelegates.Count);
            Assert.AreEqual(Network.Main, this.fullNodeBuilder.Network);
            Assert.AreEqual(settings, this.fullNodeBuilder.NodeSettings);
        }

        [TestMethod]
        public void ConfigureServicesAddsServiceToDelegatesList()
        {
            var action = new Action<IServiceCollection>(e => { e.AddSingleton<IServiceProvider>(); });

            var result = this.fullNodeBuilder.ConfigureServices(action);

            Assert.AreEqual(1, this.serviceCollectionDelegates.Count);
            Assert.AreEqual(action, this.serviceCollectionDelegates[0]);
            Assert.AreEqual(this.fullNodeBuilder, result);
        }

        [TestMethod]
        public void ConfigureFeatureAddsFeatureToDelegatesList()
        {
            var action = new Action<IFeatureCollection>(e => { var registrations = e.FeatureRegistrations; });

            var result = this.fullNodeBuilder.ConfigureFeature(action);

            Assert.AreEqual(1, this.featureCollectionDelegates.Count);
            Assert.AreEqual(action, this.featureCollectionDelegates[0]);
            Assert.AreEqual(this.fullNodeBuilder, result);
        }

        [TestMethod]
        public void ConfigureServiceProviderAddsServiceProviderToDelegatesList()
        {
            var action = new Action<IServiceProvider>(e => { var serv = e.GetService(typeof(string)); });

            var result = this.fullNodeBuilder.ConfigureServiceProvider(action);

            Assert.AreEqual(1, this.serviceProviderDelegates.Count);
            Assert.AreEqual(action, this.serviceProviderDelegates[0]);
            Assert.AreEqual(this.fullNodeBuilder, result);
        }

        [TestMethod]
        public void BuildWithInitialServicesSetupConfiguresFullNodeUsingConfiguration()
        {
            var nodeSettings = new NodeSettings();
            nodeSettings.DataDir = "TestData/FullNodeBuilder/BuildWithInitialServicesSetup";

            this.fullNodeBuilder = new FullNodeBuilder(nodeSettings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);

            this.fullNodeBuilder.ConfigureServices(e =>
            {
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

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void BuildConfiguresFullNodeUsingConfiguration()
        {
            var nodeSettings = new NodeSettings();
            nodeSettings.DataDir = "TestData/FullNodeBuilder/BuildConfiguresFullNodeUsingConfiguration";

            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton(nodeSettings);
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

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void BuildWithoutFullNodeInServiceConfigurationThrowsException()
        {
            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton<NodeSettings>();
                e.AddSingleton<Network>(NodeSettings.Default().GetNetwork());
            });

            this.fullNodeBuilder.Build();
            this.fullNodeBuilder.Build();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void BuildTwiceThrowsException()
        {
            var nodeSettings = new NodeSettings();
            nodeSettings.DataDir = "TestData/FullNodeBuilder/BuildConfiguresFullNodeUsingConfiguration";

            this.fullNodeBuilder.ConfigureServices(e =>
            {
                e.AddSingleton(nodeSettings);
                e.AddSingleton(nodeSettings.GetNetwork());
                e.AddSingleton<FullNode>();
            });

            this.fullNodeBuilder.Build();
            this.fullNodeBuilder.Build();
        }

        [TestMethod]
        [ExpectedException(typeof(NodeBuilderException))]
        public void BuildWithoutNodeSettingsInServiceConfigurationThrowsException()
        {
            this.fullNodeBuilder.Build();
        }

        [TestMethod]
        public void CanHaveAllServicesTest()
        {
            var nodeSettings = NodeSettings.Default();
            nodeSettings.DataDir = "Stratis.Bitcoin.Tests/TestData/FullNodeBuilderTest/CanHaveAllServicesTest";
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UseConsensus()
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .Build();

            IServiceProvider serviceProvider = fullNode.Services.ServiceProvider;
            var network = serviceProvider.GetService<Network>();
            var settings = serviceProvider.GetService<NodeSettings>();
            var consensusLoop = serviceProvider.GetService<ConsensusLoop>();
            var consensus = serviceProvider.GetService<PowConsensusValidator>();
            var chain = serviceProvider.GetService<NBitcoin.ConcurrentChain>();
            var chainState = serviceProvider.GetService<ChainBehavior.ChainState>();
            var blockStoreManager = serviceProvider.GetService<BlockStoreManager>();
            var mempoolManager = serviceProvider.GetService<MempoolManager>();
            var connectionManager = serviceProvider.GetService<ConnectionManager>();

            Assert.IsNotNull(fullNode);
            Assert.IsNotNull(network);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(consensusLoop);
            Assert.IsNotNull(consensus);
            Assert.IsNotNull(chain);
            Assert.IsNotNull(chainState);
            Assert.IsNotNull(blockStoreManager);
            Assert.IsNotNull(mempoolManager);
        }

    }
}
