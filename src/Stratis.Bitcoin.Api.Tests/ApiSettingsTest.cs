using System;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    /// <summary>
    /// Tests the settings for the API features.
    /// </summary>
    public class ApiSettingsTest : TestBase
    {
        public ApiSettingsTest() : base(Network.Main)
        {
        }

        /// <summary>
        /// Tests that if no API settings are passed and we're on the bitcoin network, the defaults settings are used.
        /// </summary>
        [Fact]
        public void GivenNoApiSettingsAreProvided_AndOnBitcoinNetwork_ThenDefaultSettingAreUsed()
        {
            // Arrange.
            Network network = Network.Main;
            NodeSettings nodeSettings = new NodeSettings(network, loadConfiguration:false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultBitcoinApiPort, settings.ApiPort);
            Assert.Equal(new Uri($"{ApiSettings.DefaultApiHost}:{ApiSettings.DefaultBitcoinApiPort}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if no API settings are passed and we're on the Stratis network, the defaults settings are used.
        /// </summary>
        [Fact]
        public void GivenNoApiSettingsAreProvided_AndOnStratisNetwork_ThenDefaultSettingAreUsed()
        {
            // Arrange.
            Network network = Network.StratisMain;
            NodeSettings nodeSettings = new NodeSettings(network, loadConfiguration:false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultStratisApiPort, settings.ApiPort);
            Assert.Equal(new Uri($"{ApiSettings.DefaultApiHost}:{ApiSettings.DefaultStratisApiPort}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if a custom API port is passed, the port is used in conjunction with the default API URI.
        /// </summary>
        [Fact]
        public void GivenApiPortIsProvided_ThenPortIsUsedWithDefaultApiUri()
        {
            // Arrange.
            int customPort = 55555;
            NodeSettings nodeSettings = new NodeSettings(args:new[] { $"-apiport={customPort}" }, loadConfiguration: false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(customPort, settings.ApiPort);
            Assert.Equal(new Uri($"{ApiSettings.DefaultApiHost}:{customPort}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if a custom API URI is passed and we're on the bitcoin network, the bitcoin port is used in conjunction with the passed API URI.
        /// </summary>
        [Fact]
        public void GivenApiUriIsProvided_AndGivenBitcoinNetwork_ThenApiUriIsUsedWithDefaultBitcoinApiPort()
        {
            // Arrange.
            string customApiUri = "http://0.0.0.0";
            Network network = Network.Main;
            NodeSettings nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}" }, loadConfiguration:false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultBitcoinApiPort, settings.ApiPort);
            Assert.Equal(new Uri($"{customApiUri}:{ApiSettings.DefaultBitcoinApiPort}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if a custom API URI is passed and we're on the Stratis network, the bitcoin port is used in conjunction with the passed API URI.
        /// </summary>
        [Fact]
        public void GivenApiUriIsProvided_AndGivenStratisNetwork_ThenApiUriIsUsedWithDefaultStratisApiPort()
        {
            // Arrange.
            string customApiUri = "http://0.0.0.0";
            Network network = Network.StratisMain;
            NodeSettings nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}" }, loadConfiguration:false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultStratisApiPort, settings.ApiPort);
            Assert.Equal(new Uri($"{customApiUri}:{ApiSettings.DefaultStratisApiPort}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if a custom API URI and a custom API port are passed, both are used in conjunction to make the API URI.
        /// </summary>
        [Fact]
        public void GivenApiUri_AndApiPortIsProvided_AndGivenBitcoinNetwork_ThenApiUriIsUsedWithApiPort()
        {
            // Arrange.
            string customApiUri = "http://0.0.0.0";
            int customPort = 55555;
            Network network = Network.Main;
            NodeSettings nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}", $"-apiport={customPort}" }, loadConfiguration:false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(customPort, settings.ApiPort);
            Assert.Equal(new Uri($"{customApiUri}:{customPort}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if a custom API URI is passed and we're on the Stratis network, the bitcoin port is used in conjunction with the passed API URI.
        /// </summary>
        [Fact]
        public void GivenApiUriIncludingPortIsProvided_ThenUseThePassedApiUri()
        {
            // Arrange.
            int customPort = 5522;
            string customApiUri = $"http://0.0.0.0:{customPort}";
            Network network = Network.Main;
            NodeSettings nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}" }, loadConfiguration:false);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(customPort, settings.ApiPort);
            Assert.Equal(new Uri($"{customApiUri}"), settings.ApiUri);
        }

        /// <summary>
        /// Tests that if we're on the Bitcoin main network, the port used in the API is the right one.
        /// </summary>
        [Fact]
        public void GivenBitcoinMain_ThenUseTheCorrectPort()
        {
            // Arrange.
            NodeSettings nodeSettings = NodeSettings.Default(Network.Main);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultBitcoinApiPort, settings.ApiPort);
        }

        /// <summary>
        /// Tests that if we're on the Bitcoin test network, the port used in the API is the right one.
        /// </summary>
        [Fact]
        public void GivenBitcoinTestnet_ThenUseTheCorrectPort()
        {
            // Arrange.
            NodeSettings nodeSettings = NodeSettings.Default(Network.TestNet);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.TestBitcoinApiPort, settings.ApiPort);
        }

        /// <summary>
        /// Tests that if we're on the Stratis main network, the port used in the API is the right one.
        /// </summary>
        [Fact]
        public void GivenStratisMainnet_ThenUseTheCorrectPort()
        {
            // Arrange.
            NodeSettings nodeSettings = NodeSettings.Default(Network.StratisMain);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultStratisApiPort, settings.ApiPort);
        }

        /// <summary>
        /// Tests that if we're on the Stratis test network, the port used in the API is the right one.
        /// </summary>
        [Fact]
        public void GivenStratisTestnet_ThenUseTheCorrectPort()
        {
            // Arrange.
            NodeSettings nodeSettings = NodeSettings.Default(Network.StratisTest);

            // Act.
            ApiSettings settings = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .Build()
                .NodeService<ApiSettings>();
            settings.Load(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.TestStratisApiPort, settings.ApiPort);
        }
    }
}
