using System;
using System.Net;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCAuthorizationTest
    {
        private RPCAuthorization authorization;

        public RPCAuthorizationTest()
        {
            this.authorization = new RPCAuthorization();
        }

        [Fact]
        public void IsAuthorizedWithLowerCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            bool result = this.authorization.IsAuthorized("testuser");

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithCamelCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            bool result = this.authorization.IsAuthorized("testUser");

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithUpperCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            bool result = this.authorization.IsAuthorized("TESTUSER");

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithUserNotOnListReturnsFalse()
        {
            this.authorization.Authorized.Add("TestUser");

            bool result = this.authorization.IsAuthorized("newuser");

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedWithEmptyListReturnsTrue()
        {
            bool result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithIPAddressOnListReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.1.1.15"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpWithAllZerosIPAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("0.0.0.0/0"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpWithAllZerosIPV6AddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("0:0:0:0:0:0:0:0/0"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("1:2:3:4:5:6:7:8"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpWithIPInBlockAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("240.0.0.0/4"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("242.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpWithIPNotInBlockAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("208.0.0.0/4"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("242.1.1.15"));

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedIpWithIPInV6BlockAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("0:0:0:0:0:ffff:f000:0/100"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("242.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpWithIPNotInV6BlockAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("0:0:0:0:0:ffff:d000:0/100"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("242.1.1.15"));

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedIpV6WithIPInBlockAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("240.0.0.0/4"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("0:0:0:0:0:ffff:f201:010f"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpV6WithIPNotInBlockAddressReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddressBlock.Parse("208.0.0.0/4"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("0:0:0:0:0:ffff:f201:010f"));

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedIpNotOnListReturnsFalse()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.0.0.1"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedInvalidBlockThrowsError()
        {
            Assert.Throws<FormatException>(() => IPAddressBlock.Parse("240.0.0.0/33"));
        }
    }
}
