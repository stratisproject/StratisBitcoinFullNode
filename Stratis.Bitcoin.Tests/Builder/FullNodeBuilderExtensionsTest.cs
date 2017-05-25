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
		}

		[Fact]
		public void UseNodeSettingsConfiguresNodeBuilderWithNodeSettings()
		{
			Logs.Configure(new LoggerFactory());

			FullNodeBuilderExtensions.UseDefaultNodeSettings(this.fullNodeBuilder);

			Assert.NotNull(this.fullNodeBuilder.NodeSettings);
			Assert.Equal(NodeSettings.Default().ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
			Assert.Equal(NodeSettings.Default().DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
			Assert.NotNull(this.fullNodeBuilder.Network);
			Assert.Equal(NodeSettings.Default().GetNetwork(),this.fullNodeBuilder.Network);
			Assert.Equal(1, serviceCollectionDelegates.Count);
		}

		[Fact]
		public void UseDefaultNodeSettingsConfiguresNodeBuilderWithDefaultSettings()
		{
			Logs.Configure(new LoggerFactory());

			var nodeSettings = NodeSettings.Default();
			nodeSettings.ConfigurationFile = "TestData/FullNodeBuilder/UseNodeSettingsConfFile";
			nodeSettings.DataDir = "TestData/FullNodeBuilder/UseNodeSettings";

			FullNodeBuilderExtensions.UseNodeSettings(this.fullNodeBuilder, nodeSettings);
			
			Assert.NotNull(this.fullNodeBuilder.NodeSettings);
			Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
			Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
			Assert.NotNull(this.fullNodeBuilder.Network);
			Assert.Equal(1, serviceCollectionDelegates.Count);
		}
	}
}
