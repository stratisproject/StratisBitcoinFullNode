using System;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Api.Tests
{
    /// <summary>
    /// Tests the settings for the API features.
    /// </summary>
    public class ApiSettingsTest : TestBase
    {
        public ApiSettingsTest() : base(KnownNetworks.Main)
        {
        }

        /// <summary>
        /// Tests that if no API settings are passed and we're on the bitcoin network, the defaults settings are used.
        /// </summary>
        [Fact]
        public void GivenNoApiSettingsAreProvided_AndOnBitcoinNetwork_ThenDefaultSettingAreUsed()
        {
            // Arrange.
            Network network = KnownNetworks.Main;
            var nodeSettings = new NodeSettings(network);

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            Network network = KnownNetworks.StratisMain;
            var nodeSettings = new NodeSettings(network);

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.DefaultStratisApiPort, settings.ApiPort);
            Assert.Equal(new Uri($"{ApiSettings.DefaultApiHost}:{ApiSettings.DefaultStratisApiPort}"), settings.ApiUri);

            settings.HttpsCertificateFilePath.Should().BeNull();
            settings.UseHttps.Should().BeFalse();
        }

        /// <summary>
        /// Tests that if a custom API port is passed, the port is used in conjunction with the default API URI.
        /// </summary>
        [Fact]
        public void GivenApiPortIsProvided_ThenPortIsUsedWithDefaultApiUri()
        {
            // Arrange.
            int customPort = 55555;
            var nodeSettings = new NodeSettings(this.Network, args:new[] { $"-apiport={customPort}" });

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            Network network = KnownNetworks.Main;
            var nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}" });

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);


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
            Network network = KnownNetworks.StratisMain;
            var nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}" });

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            Network network = KnownNetworks.Main;
            var nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}", $"-apiport={customPort}" });

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            Network network = KnownNetworks.Main;
            var nodeSettings = new NodeSettings(network, args:new[] { $"-apiuri={customApiUri}" });

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            NodeSettings nodeSettings = NodeSettings.Default(KnownNetworks.Main);

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            NodeSettings nodeSettings = NodeSettings.Default(KnownNetworks.TestNet);

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            NodeSettings nodeSettings = NodeSettings.Default(KnownNetworks.StratisMain);

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

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
            NodeSettings nodeSettings = NodeSettings.Default(KnownNetworks.StratisTest);

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

            // Assert.
            Assert.Equal(ApiSettings.TestStratisApiPort, settings.ApiPort);
        }

        [Theory]
        [InlineData(true, @"https://")]
        [InlineData(false, @"http://")]
        public void GivenUseHttps_ThenUsesTheCorrectProtocol(bool useHttps, string expectedProtocolPrefix)
        {
            // Arrange.
            var nodeSettings = new NodeSettings(KnownNetworks.TestNet, args: new[] { $"-usehttps={useHttps}", "-certificatefilepath=nonNullValue" });

            // Act.
            var settings = FullNodeSetup(nodeSettings);

            // Assert.
            settings.UseHttps.Should().Be(useHttps);
            settings.ApiUri.ToString().Should().StartWith(expectedProtocolPrefix);
        }

        [Fact]
        public void GivenCertificateFilePath_ThenUsesTheCorrectFileName()
        {
            // Arrange.
            var certificateFileName = @"abcd/someCertificate.pfx";
            var nodeSettings = new NodeSettings(KnownNetworks.TestNet, args: new[] { $"-certificatefilepath={certificateFileName}" });

            // Act.
            ApiSettings settings = FullNodeSetup(nodeSettings);

            // Assert.
            settings.HttpsCertificateFilePath.Should().Be(certificateFileName);
        }

        [Fact]
        public void GivenUseHttpsAndNoCertificateFilePath_ThenShouldThrowConfigurationException()
        {
            // Arrange.
            var nodeSettings = new NodeSettings(KnownNetworks.TestNet, args: new[] { $"-usehttps={true}" });

            // Act.
            var settingsAction = new Action(() =>
                {
                    FullNodeSetup(nodeSettings);
                });

            // Assert.
            settingsAction.Should().Throw<ConfigurationException>();
        }

        private static ApiSettings FullNodeSetup(NodeSettings nodeSettings)
        {
            return new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseApi()
                .UsePowConsensus()
                .Build()
                .NodeService<ApiSettings>();
        }
    }
}
