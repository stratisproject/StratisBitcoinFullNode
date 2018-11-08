using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Tests.Connection
{
    public class ConnectionManagerSettingsTest : LogsTestBase
    {
        private readonly Mock<IConnectionManager> connectionManager;
        private ConnectionManagerController controller;
        private readonly Mock<ILoggerFactory> mockLoggerFactory;

        public ConnectionManagerSettingsTest()
        {
            this.connectionManager = new Mock<IConnectionManager>();
            this.mockLoggerFactory = new Mock<ILoggerFactory>();
            this.mockLoggerFactory.Setup(i => i.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            this.connectionManager.Setup(i => i.Network)
                .Returns(KnownNetworks.StratisTest);
            this.controller = new ConnectionManagerController(this.connectionManager.Object, this.LoggerFactory.Object);
        }

        /// <summary>
        /// 127.0.0.1:16178 == 0.0.0.0:16178
        /// </summary>
        [Fact]
        public void LocalhostEndpoint_IsRoutableTo_DefaultRouteEndpointWithSamePort()
        {
            const int samePort = 16178;

            IPAddress ipAddressNodeIsListeningOn = IPAddress.Parse("127.0.0.1");
            IPAddress ipAddressWhiteBind = IPAddress.Parse("0.0.0.0");

            var connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(this.Network));
            IPEndPoint defaultRouteEndpointWithSamePort = new IPEndPoint(ipAddressNodeIsListeningOn, samePort);
            var defaultNodeServerRouteEndpointWithSamePort = new NodeServerEndpoint(defaultRouteEndpointWithSamePort, false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { defaultNodeServerRouteEndpointWithSamePort };
            connectionManagerSettings.Port = samePort;
            IPEndPoint whiteBindEndpoint = new IPEndPoint(ipAddressWhiteBind, samePort);

            bool isRoutable = connectionManagerSettings.EndpointIsRoutableToAnotherEndpoint(whiteBindEndpoint.ToString(), out NodeServerEndpoint localEndpoint);

            Assert.True(isRoutable);
        }

        /// <summary>
        /// 127.0.0.1:16178 != 0.0.0.0:44556
        /// </summary>
        [Fact]
        public void LocalhostEndpoint_IsNotRoutableTo_DefaultRouteEndpointWithDifferentPort()
        {
            const int portNodeIsListeningOn = 16178;
            const int portWhiteBind = 44556;

            IPAddress ipAddressNodeIsListeningOn = IPAddress.Parse("127.0.0.1");
            IPAddress ipAddressWhiteBind = IPAddress.Parse("0.0.0.0");

            var connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(this.Network));
            IPEndPoint defaultRouteEndpointWithDifferentPort = new IPEndPoint(ipAddressNodeIsListeningOn, portNodeIsListeningOn);
            var defaultNodeServerRouteEndpointWithDifferentPort = new NodeServerEndpoint(defaultRouteEndpointWithDifferentPort, false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { defaultNodeServerRouteEndpointWithDifferentPort };
            connectionManagerSettings.Port = portNodeIsListeningOn;

            IPEndPoint whiteBindEndpoint = new IPEndPoint(ipAddressWhiteBind, portWhiteBind);

            bool isRoutable = connectionManagerSettings.EndpointIsRoutableToAnotherEndpoint(whiteBindEndpoint.ToString(), out NodeServerEndpoint localEndpoint);

            Assert.False(isRoutable);
        }
    }
}
