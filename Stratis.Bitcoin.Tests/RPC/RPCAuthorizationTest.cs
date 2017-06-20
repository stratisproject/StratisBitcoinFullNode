using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Stratis.Bitcoin.RPC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.RPC
{
    [TestClass]
    public class RPCAuthorizationTest
    {
        private RPCAuthorization authorization;

        [TestInitialize]
        public void Initialize()
        {
            this.authorization = new RPCAuthorization();
        }

        [TestMethod]
        public void IsAuthorizedWithLowerCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("testuser");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAuthorizedWithCamelCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("testUser");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAuthorizedWithUpperCasedUserOnListReturnsTrue()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("TESTUSER");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAuthorizedWithUserNotOnListReturnsFalse()
        {
            this.authorization.Authorized.Add("TestUser");

            var result = this.authorization.IsAuthorized("newuser");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsAuthorizedWithEmptyListReturnsTrue()
        {
            var result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAuthorizedWithIPAddressOnListReturnsTrue()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.1.1.15"));

            var result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsAuthorizedIpNotOnListReturnsFalse()
        {
            this.authorization.AllowIp.Add(IPAddress.Parse("127.0.0.1"));

            var result = this.authorization.IsAuthorized(IPAddress.Parse("127.1.1.15"));

            Assert.IsFalse(result);
        }
    }
}
