using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder
{
    public class FullNodeBuilderExtensionsTest
    {
        private FeatureCollection featureCollection;
        private List<Action<IFeatureCollection>> featureCollectionDelegates;
        private FullNodeBuilder fullNodeBuilder;
        private List<Action<IServiceCollection>> serviceCollectionDelegates;
        private List<Action<IServiceProvider>> serviceProviderDelegates;

        public FullNodeBuilderExtensionsTest()
        {
            this.serviceCollectionDelegates = new List<Action<IServiceCollection>>();
            this.serviceProviderDelegates = new List<Action<IServiceProvider>>();
            this.featureCollectionDelegates = new List<Action<IFeatureCollection>>();
            this.featureCollection = new FeatureCollection();

            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
            this.fullNodeBuilder.Network = KnownNetworks.TestNet;
        }

        [Fact]
        public void UseNodeSettingsConfiguresNodeBuilderWithNodeSettings()
        {
            FullNodeBuilderNodeSettingsExtension.UseDefaultNodeSettings(this.fullNodeBuilder);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(NodeSettings.Default(this.fullNodeBuilder.Network).ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(NodeSettings.Default(this.fullNodeBuilder.Network).DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(NodeSettings.Default(this.fullNodeBuilder.Network).Network, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseDefaultNodeSettingsConfiguresNodeBuilderWithDefaultSettings()
        {
            var nodeSettings = new NodeSettings(this.fullNodeBuilder.Network, args: new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(KnownNetworks.TestNet, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseNodeSettingsUsingTestNetConfiguresNodeBuilderWithTestnetSettings()
        {
            var nodeSettings = new NodeSettings(KnownNetworks.TestNet, args:new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(KnownNetworks.TestNet, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseNodeSettingsUsingRegTestNetConfiguresNodeBuilderWithRegTestNet()
        {
            var nodeSettings = new NodeSettings(KnownNetworks.RegTest, args:new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(KnownNetworks.RegTest, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }
    }
}
