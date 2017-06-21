using Microsoft.Extensions.DependencyInjection;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.Builder
{
    [TestClass]
    public class FullNodeBuilderExtensionsTest
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

			this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
		}

		[TestMethod]
		public void UseNodeSettingsConfiguresNodeBuilderWithNodeSettings()
		{
			Logs.Configure(new LoggerFactory());

			FullNodeBuilderExtensions.UseDefaultNodeSettings(this.fullNodeBuilder);

			Assert.IsNotNull(this.fullNodeBuilder.NodeSettings);
			Assert.AreEqual(NodeSettings.Default().ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
			Assert.AreEqual(NodeSettings.Default().DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
			Assert.IsNotNull(this.fullNodeBuilder.Network);
			Assert.AreEqual(NodeSettings.Default().GetNetwork(),this.fullNodeBuilder.Network);
			Assert.AreEqual(1, this.serviceCollectionDelegates.Count);
		}

        [TestMethod]
        public void UseDefaultNodeSettingsConfiguresNodeBuilderWithDefaultSettings()
        {
            Logs.Configure(new LoggerFactory());

            var nodeSettings = NodeSettings.Default();
            nodeSettings.ConfigurationFile = "TestData/FullNodeBuilder/UseNodeSettingsConfFile";
            nodeSettings.DataDir = "TestData/FullNodeBuilder/UseNodeSettings";
            nodeSettings.Testnet = true;

            FullNodeBuilderExtensions.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.IsNotNull(this.fullNodeBuilder.NodeSettings);
            Assert.AreEqual(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.AreEqual(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.IsNotNull(this.fullNodeBuilder.Network);
            Assert.AreEqual(Network.Main, this.fullNodeBuilder.Network);
            Assert.AreEqual(1, this.serviceCollectionDelegates.Count);
        }

        [TestMethod]
		public void UseNodeSettingsUsingTestNetConfiguresNodeBuilderWithTestnetSettings()
		{
			Logs.Configure(new LoggerFactory());

			var nodeSettings = NodeSettings.FromArguments(new string[] { "-testnet" });
			nodeSettings.ConfigurationFile = "TestData/FullNodeBuilder/UseNodeSettingsConfFile";
			nodeSettings.DataDir = "TestData/FullNodeBuilder/UseNodeSettings";			

			FullNodeBuilderExtensions.UseNodeSettings(this.fullNodeBuilder, nodeSettings);
			
			Assert.IsNotNull(this.fullNodeBuilder.NodeSettings);
			Assert.AreEqual(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
			Assert.AreEqual(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
			Assert.IsNotNull(this.fullNodeBuilder.Network);
			Assert.AreEqual(Network.TestNet, this.fullNodeBuilder.Network);
			Assert.AreEqual(1, this.serviceCollectionDelegates.Count);
		}

        [TestMethod]
        public void UseNodeSettingsUsingRegTestNetConfiguresNodeBuilderWithRegTestNet()
        {
            Logs.Configure(new LoggerFactory());

            var nodeSettings = NodeSettings.FromArguments(new string[] { "-regtest" });
            nodeSettings.ConfigurationFile = "TestData/FullNodeBuilder/UseNodeSettingsConfFile";
            nodeSettings.DataDir = "TestData/FullNodeBuilder/UseNodeSettings";

            FullNodeBuilderExtensions.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.IsNotNull(this.fullNodeBuilder.NodeSettings);
            Assert.AreEqual(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.AreEqual(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.IsNotNull(this.fullNodeBuilder.Network);
            Assert.AreEqual(Network.RegTest, this.fullNodeBuilder.Network);
            Assert.AreEqual(1, this.serviceCollectionDelegates.Count);
        }
    }
}
