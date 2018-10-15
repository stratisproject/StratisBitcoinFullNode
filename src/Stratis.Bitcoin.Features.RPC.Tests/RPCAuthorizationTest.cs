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
        public void IsAuthorizedIpNotOnListReturnsFalse()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.0.0.1"));

            bool result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedWhenIPv6AnyAllowedReturnsTrueForEverything()
        {
            this.authorization.AllowIp.Add(IPAddress.IPv6Any);

            Assert.True(this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15")));
            Assert.True(this.authorization.IsAuthorized(IPAddress.Parse("4.5.6.7")));
            Assert.True(this.authorization.IsAuthorized(IPAddress.Parse("::")));
            Assert.True(this.authorization.IsAuthorized(IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334")));
        }

        [Fact]
        public void IsAuthorizedWhenIPv4AnyAllowedReturnsTrueForIPv4FalseForIPv6()
        {
            this.authorization.AllowIp.Add(IPAddress.Any);

            Assert.True(this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15")));
            Assert.False(this.authorization.IsAuthorized(IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334")));
        }
    }
}
