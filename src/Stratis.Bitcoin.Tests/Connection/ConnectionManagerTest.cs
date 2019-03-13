using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities.Extensions;
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
            IPEndPoint endpointA = null;
            IPEndPoint endpointB = null;
            IPEndPoint endpointOut = null;

            var connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(this.Network));
            var networkEndpoints = connectionManagerSettings.Bind.Select(x => x.Endpoint).ToList();

            // IPV4: 127.0.0.1:16178 != 0.0.0.0:16178 (both are considered local endpoints)
            endpointA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178);
            networkEndpoints = new List<IPEndPoint>() { endpointB};
            connectionManagerSettings.Port = 16178;
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // IPV6: [::1]:16178 != [::]:16178
            endpointA = new IPEndPoint(IPAddress.Parse("[::1]"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("[::]"), 16178);
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            connectionManagerSettings.Port = 16178;
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // 127.0.0.1:16178 != 0.0.0.0:44556
            endpointA = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 44556);
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            connectionManagerSettings.Port = 44556;
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // 0.0.0.0:16178 == 127.0.0.1:16178
            endpointA = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16178);
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            connectionManagerSettings.Port = 16178;
            Assert.True(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // IPV4: 0.0.0.0:16178 == 127.0.0.2:16178
            endpointA = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("127.0.0.2"), 16178);
            connectionManagerSettings.Port = 16178;
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            Assert.True(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // IPV6: [::]:16178 == [::1]:16178
            endpointA = new IPEndPoint(IPAddress.Parse("[::]"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("[::1]"), 16178);
            connectionManagerSettings.Port = 16178;
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            Assert.True(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // IPV4: 0.0.0.0:16178 == 127.0.0.2:999
            endpointA = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("127.0.0.2"), 999);
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            connectionManagerSettings.Port = 999;
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // IPV6: [::]:16178 != [::2]:999
            endpointA = new IPEndPoint(IPAddress.Parse("[::]"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("[::2]"), 999);
            connectionManagerSettings.Port = 999;
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // IPV6: [::1] != [fe80::d111:a4c4:ce4:2bc7%21] (Localhost -> Network address).
            endpointA = new IPEndPoint(IPAddress.Parse("[::1]"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("[fe80::d111:a4c4:ce4:2bc7%21]"), 16178);
            connectionManagerSettings.Port = 16178;
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // 10.0.0.1:16178 != 192.168.1.1:16178 (Bound to any but whitelisting local).
            endpointA = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 16178);
            networkEndpoints = new List<IPEndPoint>() { endpointB };
            connectionManagerSettings.Port = 16178;
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // Using whitebind = 0.0.0.0:16178 and connecting from local or remote IP address on port 16178 should be whitebinded.
            endpointA = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 16178);
            Assert.True(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // Using whitebind = 100.64.1.1:16178 and connecting from local or remote IP address(other than 100.64.1.1) on port 16178 should not be whitebinded.
            endpointA = new IPEndPoint(IPAddress.Parse("100.64.1.1"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("100.64.1.2"), 16178);
            Assert.False(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));

            // Using whitebind = 0.0.0.0:16178 and connecting from local or remote IP address on port other than 16178 should be not whitebinded.
            endpointA = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 16178);
            endpointB = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16179);
            Assert.True(endpointA.CanBeMappedTo(networkEndpoints, out endpointOut));
        }
    }
}
