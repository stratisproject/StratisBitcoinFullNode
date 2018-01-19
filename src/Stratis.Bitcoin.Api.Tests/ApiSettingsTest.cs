﻿using System;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Tests;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    /// <summary>
    /// Tests the settings for the API features.
    /// The default settings are:
    /// ApiPort: 37220 for bitcoin, 37221 for Stratis.
    /// ApiUri: http://localhost:37220 or http://localhost:37221.
    /// </summary>
    public class ApiSettingsTest : TestBase
    {
        public ApiSettingsTest()
        {
            // These flags are being set on an individual test case basis.
            // Assume the default values for the static flags.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

        /// <summary>
        /// Tests that if no API settings are passed and we're on the bitcoin network, the defaults settings are used.
        /// </summary>
        [Fact]
        public void GivenNoApiSettingsAreProvided_AndOnBitcoinNetwork_ThenDefaultSettingAreUsed()
        {
            // Arrange.
            Network network = Network.Main;
            NodeSettings nodeSettings = new NodeSettings("bitcoin", network).LoadArguments(new string[0]);

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
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
            Network network = Network.StratisMain;
            NodeSettings nodeSettings = new NodeSettings("stratis", network).LoadArguments(new string[0]);

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
            NodeSettings nodeSettings = new NodeSettings().LoadArguments(new[] { $"-apiport={customPort}" });

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
            NodeSettings nodeSettings = new NodeSettings("bitcoin", network).LoadArguments(new[] { $"-apiuri={customApiUri}" });

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
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
            string customApiUri = "http://0.0.0.0";
            Network network = Network.StratisMain;
            NodeSettings nodeSettings = new NodeSettings("stratis", network).LoadArguments(new[] { $"-apiuri={customApiUri}" });

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
            NodeSettings nodeSettings = new NodeSettings("bitcoin", network).LoadArguments(new[] { $"-apiuri={customApiUri}", $"-apiport={customPort}" });

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
            NodeSettings nodeSettings = new NodeSettings("bitcoin", network).LoadArguments(new[] { $"-apiuri={customApiUri}" });

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
    }
}
