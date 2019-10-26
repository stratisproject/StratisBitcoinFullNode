using System;
using System.Net;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities.Extensions
{
    public class IPExtensionsTest
    {
        [Fact]
        public void CheckConvertingEmptyIPAddressToEndpoint()
        {
            // Assert
            Assert.Throws<FormatException>(() =>
            {
                // Act
                IPEndPoint endpoint = "".ToIPEndPoint(1234);
            });
        }

        [Fact]
        public void CheckConvertingIPAddressWithInvalidPortNumberToEndpoint()
        {
            // Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                // Act
                IPEndPoint endpoint = "0.0.0.0".ToIPEndPoint(IPEndPoint.MaxPort + 1);
            });
        }

        [Fact]
        public void CheckConvertingIPAddressWithInvalidParsedPortNumberToEndpoint()
        {
            // Assert
            Assert.Throws<FormatException>(() =>
            {
                // Act
                IPEndPoint endpoint = "0.0.0.0:454z".ToIPEndPoint(1234);
            });
        }

        [Fact]
        public void CheckConvertingIPv4AddressToEndpoint()
        {
            // Act
            IPEndPoint endpoint = "15.61.23.23".ToIPEndPoint(1234);

            // Assert
            Assert.Equal(1234, endpoint.Port);
            Assert.Equal("15.61.23.23", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPv4AddressWithPortToEndpoint()
        {
            // Act
            IPEndPoint endpoint = "15.61.23.23:1500".ToIPEndPoint(1234);

            // Assert
            Assert.Equal(1500, endpoint.Port);
            Assert.Equal("15.61.23.23", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPv6AddressToEndpoint()
        {
            // Act
            IPEndPoint endpoint = "[1233:3432:2434:2343:3234:2345:6546:4534]".ToIPEndPoint(1234);

            // Assert
            Assert.Equal(1234, endpoint.Port);
            Assert.Equal("1233:3432:2434:2343:3234:2345:6546:4534", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPv6AddressWithPortToEndpoint()
        {
            // Act
            IPEndPoint endpoint = "[1233:3432:2434:2343:3234:2345:6546:4534]:5443".ToIPEndPoint(1234);

            // Assert
            Assert.Equal(5443, endpoint.Port);
            Assert.Equal("1233:3432:2434:2343:3234:2345:6546:4534", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPEndPointStringToEndpoint()
        {
            // Act
            IPEndPoint endpoint = "::ffff:192.168.4.1".ToIPEndPoint(1234);

            // Assert
            Assert.Equal(1234, endpoint.Port);
            Assert.Equal("::ffff:192.168.4.1", endpoint.Address.ToString());
        }

        [Fact]
        public void CheckConvertingIPEndPointStringWithPortToEndpoint()
        {
            // Act
            IPEndPoint endpoint = "::ffff:192.168.4.1:80".ToIPEndPoint(1234);

            // Assert
            Assert.Equal(80, endpoint.Port);
            Assert.Equal("::ffff:192.168.4.1", endpoint.Address.ToString());
        }
    }
}
