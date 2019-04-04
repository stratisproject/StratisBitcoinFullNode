using System.IO;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeConfiguration
{
    public class NodeSettingsTest : TestBase
    {
        public NodeSettingsTest():base(KnownNetworks.Main)
        {
        }

        /// <summary>
        /// Assert that a setting can be read from the command line.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadSingleValueFromCmdLine()
        {
            // Arrange
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { "-agentprefix=abc" });
            // Act
            string result = nodeSettings.ConfigReader.GetOrDefault("agentprefix", string.Empty);
            // Assert
            Assert.Equal("abc", result);
        }

        /// <summary>
        /// Assert that a setting can be read from the configuration file.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadSingleValueFromFile()
        {
            // Arrange
            string dataDir = TestBase.CreateDataFolder(this).RootPath;
            string configFile = Path.Combine(dataDir, "config.txt");
            File.WriteAllText(configFile, "agentprefix=def");
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}", $"-conf=config.txt" });
            // Act
            string result = nodeSettings.ConfigReader.GetOrDefault("agentprefix", string.Empty);
            // Assert
            Assert.Equal("def", result);
        }

        /// <summary>
        /// If both the commandline and the configuration file supplies a setting then the command line value is taken.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadSingleValueFromCmdLineAndFile()
        {
            // Arrange
            string dataDir = TestBase.CreateDataFolder(this).RootPath;
            string configFile = Path.Combine(dataDir, "config.txt");
            File.WriteAllText(configFile, "agentprefix=def");
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}", $"-conf=config.txt", "-agentprefix=abc" });
            // Act
            string result = nodeSettings.ConfigReader.GetOrDefault("agentprefix", string.Empty);
            // Assert
            Assert.Equal("abc", result);
        }

        /// <summary>
        /// If neither the commandline nor the configuration file supplies a setting then the default value is used.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadSingleValueFromNone()
        {
            // Arrange
            string dataDir = TestBase.CreateDataFolder(this).RootPath;
            string configFile = Path.Combine(dataDir, "config.txt");
            File.WriteAllText(configFile, "");
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}", $"-conf=config.txt" });
            // Act
            string result = nodeSettings.ConfigReader.GetOrDefault("agentprefix", string.Empty);
            // Assert
            Assert.Equal(string.Empty, result);
        }


        /// <summary>
        /// Assert that a multi-value setting can be read from the command line.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadMultiValueFromCmdLine()
        {
            // Arrange
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { "-addnode=0.0.0.0", "-addnode=0.0.0.1" });
            // Act
            string[] result = nodeSettings.ConfigReader.GetAll("addnode");
            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("0.0.0.0", result[0]);
            Assert.Equal("0.0.0.1", result[1]);
        }

        /// <summary>
        /// Assert that a multi-value setting can be read from the configuration file.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadMultiValueFromFile()
        {
            // Arrange
            string dataDir = TestBase.CreateDataFolder(this).RootPath;
            string configFile = Path.Combine(dataDir, "config.txt");
            File.WriteAllText(configFile, "addnode=0.0.0.0\r\naddnode=0.0.0.1");
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}", $"-conf=config.txt" });
            // Act
            string[] result = nodeSettings.ConfigReader.GetAll("addnode");
            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("0.0.0.0", result[0]);
            Assert.Equal("0.0.0.1", result[1]);
        }

        /// <summary>
        /// If both the commandline and the configuration file supplies a setting then both are combined.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadMultiValueFromCmdLineAndFile()
        {
            // Arrange
            string dataDir = TestBase.CreateDataFolder(this).RootPath;
            string configFile = Path.Combine(dataDir, "config.txt");
            File.WriteAllText(configFile, "addnode=0.0.0.0");
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}", $"-conf=config.txt", "-addnode=0.0.0.1" });
            // Act
            string[] result = nodeSettings.ConfigReader.GetAll("addnode");
            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("0.0.0.1", result[0]); // Command-line first
            Assert.Equal("0.0.0.0", result[1]);
        }

        /// <summary>
        /// If neither the commandline nor the configuration file supplies a multi-value setting then no values are returned.
        /// </summary>
        [Fact]
        public void NodeSettings_CanReadMultiValueFromNone()
        {
            // Arrange
            string dataDir = TestBase.CreateDataFolder(this).RootPath;
            string configFile = Path.Combine(dataDir, "config.txt");
            File.WriteAllText(configFile, "");
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}", $"-conf=config.txt" });
            // Act
            string[] result = nodeSettings.ConfigReader.GetAll("addnode");
            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Verifies API port value can be passed in on startup.
        /// </summary>
        [Fact]
        public void NodeSettings_CanOverrideOnlyApiPort()
        {
            const int apiport = 12345;

            var nodeSettings = new NodeSettings(new BitcoinRegTest(), args: new[] {  $"-apiport={apiport}" });

            var apiSettings = new ApiSettings(nodeSettings);
            var rpcSettings = new RpcSettings(nodeSettings);
            var configurationManagerSettings = new ConnectionManagerSettings(nodeSettings);

            Assert.Equal(apiport, apiSettings.ApiPort);
            Assert.Equal(nodeSettings.Network.DefaultRPCPort, rpcSettings.RPCPort);
            Assert.Equal(nodeSettings.Network.DefaultPort, configurationManagerSettings.Port);
        }

        /// <summary>
        /// Verifies all port values can be passed in on startup.
        /// </summary>
        [Fact]
        public void NodeSettings_CanOverrideAllPorts()
        {
            // On MacOS ports below 1024 are privileged, and cannot be bound to by anyone other than root.
            const int port = 1024 + 123;
            const int rpcPort = 1024 + 456;
            const int apiPort = 1024 + 567;

            var args = new [] {$"-port={port.ToString()}", $"-rpcport={rpcPort.ToString()}", $"-apiport={apiPort.ToString()}"};

            var nodeSettings = new NodeSettings(new BitcoinRegTest(), args: args);

            var apiSettings = new ApiSettings(nodeSettings);
            var rpcSettings = new RpcSettings(nodeSettings);
            var configurationManagerSettings = new ConnectionManagerSettings(nodeSettings);

            Assert.Equal(apiPort, apiSettings.ApiPort);
            Assert.Equal(rpcPort, rpcSettings.RPCPort);
            Assert.Equal(port, configurationManagerSettings.Port);
        }
    }
}
