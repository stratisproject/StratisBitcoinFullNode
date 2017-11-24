using System.Net;
using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeConfiguration
{
    public class NodeSettingsTest
    {
        [Fact]
        public void CheckConvertingIPv4AddressToEndpoint()
        {
            // Act
            IPEndPoint endpoint = NodeSettings.ConvertIpAddressToEndpoint("15.61.23.23", 1234);

            // Assert
            Assert.Equal(1234, endpoint.Port);
            Assert.Equal("15.61.23.23", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPv4AddressWithPortToEndpoint()
        {
            // Act
            IPEndPoint endpoint = NodeSettings.ConvertIpAddressToEndpoint("15.61.23.23:1500", 1234);

            // Assert
            Assert.Equal(1500, endpoint.Port);
            Assert.Equal("15.61.23.23", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPv6AddressToEndpoint()
        {
            // Act
            IPEndPoint endpoint = NodeSettings.ConvertIpAddressToEndpoint("[1233:3432:2434:2343:3234:2345:6546:4534]", 1234);

            // Assert
            Assert.Equal(1234, endpoint.Port);
            Assert.Equal("1233:3432:2434:2343:3234:2345:6546:4534", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPv6AddressWithPortToEndpoint()
        {
            // Act
            IPEndPoint endpoint = NodeSettings.ConvertIpAddressToEndpoint("[1233:3432:2434:2343:3234:2345:6546:4534]:5443", 1234);

            // Assert
            Assert.Equal(5443, endpoint.Port);
            Assert.Equal("1233:3432:2434:2343:3234:2345:6546:4534", endpoint.Address.ToString());
        }
    }
}
