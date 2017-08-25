﻿using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using Stratis.Bitcoin.Features.BlockStore;
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

			Assert.Equal(0, this.featureCollection.FeatureRegistrations.Count);
			Assert.Equal(0, this.featureCollectionDelegates.Count);
			Assert.Equal(0, this.serviceProviderDelegates.Count);
			Assert.Equal(0, this.serviceCollectionDelegates.Count);
			Assert.Null(this.fullNodeBuilder.Network);
			Assert.Null(this.fullNodeBuilder.NodeSettings);
		}

		[Fact]
		public void ConstructorWithNodeSettingsSetupBaseServices()
		{
			var settings = new NodeSettings();

			this.fullNodeBuilder = new FullNodeBuilder(settings, this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
			
			Assert.Equal(0, this.featureCollection.FeatureRegistrations.Count);
			Assert.Equal(1, this.featureCollectionDelegates.Count);
			Assert.Equal(0, this.serviceProviderDelegates.Count);
			Assert.Equal(1, this.serviceCollectionDelegates.Count);
			Assert.Equal(Network.Main, this.fullNodeBuilder.Network);
			Assert.Equal(settings, this.fullNodeBuilder.NodeSettings);
		}

		[Fact]
		public void ConfigureServicesAddsServiceToDelegatesList()
		{
			var action = new Action<IServiceCollection>(e => { e.AddSingleton<IServiceProvider>(); });

			var result = this.fullNodeBuilder.ConfigureServices(action);

			Assert.Equal(1, this.serviceCollectionDelegates.Count);
			Assert.Equal(action, this.serviceCollectionDelegates[0]);
			Assert.Equal(this.fullNodeBuilder, result);
		}

		[Fact]
		public void ConfigureFeatureAddsFeatureToDelegatesList()
		{
			var action = new Action<IFeatureCollection>(e => { var registrations = e.FeatureRegistrations; });

			var result = this.fullNodeBuilder.ConfigureFeature(action);

			Assert.Equal(1, this.featureCollectionDelegates.Count);
			Assert.Equal(action, this.featureCollectionDelegates[0]);
			Assert.Equal(this.fullNodeBuilder, result);
		}

		[Fact]
		public void ConfigureServiceProviderAddsServiceProviderToDelegatesList()
		{
			var action = new Action<IServiceProvider>(e => { var serv = e.GetService(typeof(string)); });

			var result = this.fullNodeBuilder.ConfigureServiceProvider(action);

			Assert.Equal(1, this.serviceProviderDelegates.Count);
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
	    public void WhenNodeSettingsIsNullUseDefault()
	    {
	        var builder = new FullNodeBuilder(null);
            Assert.Equal(Network.Main, builder.Network);
        }
    }
}
