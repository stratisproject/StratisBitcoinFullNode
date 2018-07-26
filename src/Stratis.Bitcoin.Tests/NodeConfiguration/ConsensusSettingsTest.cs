﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeConfiguration
{
    public class ConsensusSettingsTest
    {
        [Fact]
        public void LoadConfigWithAssumeValidHexLoads()
        {
            var validHexBlock = new uint256("00000000229d9fb87182d73870d53f9fdd9b76bfc02c059e6d9a6c7a3507031d");
            Network network = Networks.TestNet;
            var nodeSettings = new NodeSettings(network, args:new string[] { $"-assumevalid={validHexBlock.ToString()}" });
            var settings = new ConsensusSettings(nodeSettings);
            Assert.Equal(validHexBlock, settings.BlockAssumedValid);
        }

        [Fact]
        public void LoadConfigWithAssumeValidZeroSetsToNull()
        {
            var loggerFactory = new LoggerFactory();
            Network network = Networks.TestNet;
            var nodeSettings = new NodeSettings(network, args:new string[] { "-assumevalid=0" });
            var settings = new ConsensusSettings(nodeSettings);
            Assert.Null(settings.BlockAssumedValid);
        }

        [Fact]
        public void LoadConfigWithInvalidAssumeValidThrowsConfigException()
        {
            var loggerFactory = new LoggerFactory();
            Network network = Networks.TestNet;
            var nodeSettings = new NodeSettings(network, args:new string[] { "-assumevalid=xxx" });
            Assert.Throws<ConfigurationException>(() => new ConsensusSettings(nodeSettings));
        }

        [Fact]
        public void LoadConfigWithDefaultsSetsToNetworkDefault()
        {
            Network network = Networks.StratisMain;
            var settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = Networks.StratisTest;
            settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = Networks.Main;
            settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = Networks.TestNet;
            settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);
        }
    }
}
