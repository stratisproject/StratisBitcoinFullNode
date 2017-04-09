using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Stratis.Bitcoin.Tests.RPC.Models
{
    public class GetInfoModelTest : BaseRPCModelTest
    {
        static readonly string[] AllPropertyNames = new string[] {
                "version",
                "protocolversion",
                "walletversion",
                "balance",
                "blocks",
                "timeoffset",
                "connections",
                "proxy",
                "difficulty",
                "testnet",
                "keypoololdest",
                "keypoolsize",
                "unlocked_until",
                "paytxfee",
                "relayfee",
                "errors",
            };

        private static readonly string[] RequiredPropertyNames = new string[] {
                "version",
                "protocolversion",
                "blocks",
                "timeoffset",
                "proxy",
                "difficulty",
                "testnet",
                "relayfee",
                "errors",
            };

        [Fact]
        public void GetInfoOrderFullTest()
        {
            var expectedOrderedPropertyNames = AllPropertyNames;
            var info = new GetInfoModel()
            {
                connections = 0,
                walletversion = new object(),
                balance = new object(),
                keypoololdest = new object(),
                keypoolsize = new object(),
                unlocked_until = new object(),
                paytxfee = new object(),
            };

            JObject obj = ModelToJObject(info);
            Assert.True(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name);

            Assert.Equal(expectedOrderedPropertyNames, actualOrderedPropertyNames);

        }

        [Fact]
        public void GetInfoOrderSparseTest()
        {
            var expectedOrderedPropertyNames = RequiredPropertyNames;
            var info = new GetInfoModel();

            JObject obj = ModelToJObject(info);
            Assert.True(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name);

            Assert.Equal(expectedOrderedPropertyNames, actualOrderedPropertyNames);

        }
    }
}
