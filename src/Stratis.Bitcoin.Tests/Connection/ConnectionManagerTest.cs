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

        [Fact]
        public void WhiteListedEndpoint_CanBeMappedAndRoutedTo_OtherNode()
        {
            NodeServerEndpoint endpointA = null;
            NodeServerEndpoint endpointB = null;
            NodeServerEndpoint endpointOut = null;
            
            var connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(this.Network));

            // 127.0.0.1:16178 == 0.0.0.0:16178
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178), false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB};
            connectionManagerSettings.Port = 16178;
            Assert.True(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // 127.0.0.1:16178 != 0.0.0.0:44556
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 44556), false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            connectionManagerSettings.Port = 44556;
            Assert.False(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // 0.0.0.0:16178 == 127.0.0.1:16178
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16178), false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            connectionManagerSettings.Port = 16178;
            Assert.True(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // IPV4: 0.0.0.0:16178 == 127.0.0.2:16178
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 16178), false);
            connectionManagerSettings.Port = 16178;
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            Assert.True(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // IPV6: [::]:16178 == [::1]:16178
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("[::]"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("[::1]"), 16178), false);
            connectionManagerSettings.Port = 16178;
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            Assert.True(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // IPV4: 0.0.0.0:16178 == 127.0.0.2:999
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 999), false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            connectionManagerSettings.Port = 999;
            Assert.False(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // IPV6: [::]:16178 != [::2]:999
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("[::]"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("[::2]"), 999), false);
            connectionManagerSettings.Port = 999;
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            Assert.False(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // IPV6: [::1] != [fe80::d111:a4c4:ce4:2bc7%21] (Localhost -> Network address).
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("[::1]"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("[fe80::d111:a4c4:ce4:2bc7%21]"), 16178), false);
            connectionManagerSettings.Port = 16178;
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            Assert.False(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));

            // 10.0.0.1:16178 != 192.168.1.1:16178 (Bound to any but whitelisting local).
            endpointA = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("192.168.1.1"), 16178), false);
            endpointB = new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("10.0.0.1"), 16178), false);
            connectionManagerSettings.Listen = new List<NodeServerEndpoint>() { endpointB };
            connectionManagerSettings.Port = 16178;
            Assert.False(endpointA.CanBeMappedTo(connectionManagerSettings.Listen, out endpointOut));
        }
    }
}
