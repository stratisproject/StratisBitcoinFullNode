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

            var result = this.authorization.IsAuthorized("testuser");

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithCamelCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("testUser");

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithUpperCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("TESTUSER");

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithUserNotOnListReturnsFalse()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("newuser");

            Assert.False(result);
        }

        [Fact]
        public void IsAuthorizedWithEmptyListReturnsTrue()
        {
            var result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedWithIPAddressOnListReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.1.1.15"));

            var result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.True(result);
        }

        [Fact]
        public void IsAuthorizedIpNotOnListReturnsFalse()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.0.0.1"));

            var result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.False(result);
        }
    }
}
