using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.RPC.Models;
using Stratis.Bitcoin.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.Tests.RPC.Models
{
    [TestClass]
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

        [TestMethod]
        public void GetInfoSerializeFullTest()
        {
            var expectedOrderedPropertyNames = AllPropertyNames;
            var info = new GetInfoModel()
            {
                connections = 0,
                walletversion = default(uint),
                balance = default(decimal),
                keypoololdest = default(long),
                keypoolsize = default(int),
                unlocked_until = default(uint),
                paytxfee = default(decimal),
            };

            JObject obj = ModelToJObject(info);
            Assert.IsTrue(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name);
            
            Assert.IsTrue(expectedOrderedPropertyNames.SequenceEqual(actualOrderedPropertyNames));
        }

        [TestMethod]
        public void GetInfoSerializeSparseTest()
        {
            var expectedOrderedPropertyNames = RequiredPropertyNames;
            var info = new GetInfoModel();

            JObject obj = ModelToJObject(info);
            Assert.IsTrue(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name);

            Assert.IsTrue(expectedOrderedPropertyNames.SequenceEqual(actualOrderedPropertyNames));
        }

        [TestMethod]
        public void GetInfoDeserializeSparseTest()
        {
            IOrderedEnumerable<string> expectedSortedPropertyNames = RequiredPropertyNames.OrderBy(name => name);
            string json = "{\n" +
                         "     \"version\": 1010000,\n" +
                         "     \"protocolversion\": 70012,\n" +
                         "     \"blocks\": 460828,\n" +
                         "     \"timeoffset\": 0,\n" +
                         "     \"proxy\": \"\",\n" +
                         "     \"difficulty\": 499635929816.6675,\n" +
                         "     \"testnet\": false,\n" +
                         "     \"relayfee\": 0.00001000,\n" +
                         "     \"errors\": \"URGENT: Alert key compromised, upgrade required\"\n" +
                         "   }\n";

            JObject obj = JObject.Parse(json);
            IOrderedEnumerable<string> actualSortedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).OrderBy(name => name);

            Assert.IsTrue(expectedSortedPropertyNames.SequenceEqual(actualSortedPropertyNames));
        }

        [TestMethod]
        public void GetInfoDeserializeFullTest()
        {
            IOrderedEnumerable<string> expectedSortedPropertyNames = AllPropertyNames.OrderBy(name => name);
            string json = "{\n" +
                         "     \"version\": 1010000,\n" +
                         "     \"protocolversion\": 70012,\n" +
                         "     \"walletversion\": 60000,\n" +
                         "     \"balance\": 0.00000002,\n" +
                         "     \"blocks\": 460828,\n" +
                         "     \"timeoffset\": 0,\n" +
                         "     \"connections\": 44,\n" +
                         "     \"proxy\": \"\",\n" +
                         "     \"difficulty\": 499635929816.6675,\n" +
                         "     \"testnet\": false,\n" +
                         "     \"keypoololdest\": 1437418454,\n" +
                         "     \"keypoolsize\": 101,\n" +
                         "     \"unlocked_until\": 0,\n" +
                         "     \"paytxfee\": 0.00010000,\n" +
                         "     \"relayfee\": 0.00001000,\n" +
                         "     \"errors\": \"URGENT: Alert key compromised, upgrade required\"\n" +
                         "   }\n";

            JObject obj = JObject.Parse(json);
            IOrderedEnumerable<string> actualSortedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).OrderBy(name => name);
            GetInfoModel model = Newtonsoft.Json.JsonConvert.DeserializeObject<GetInfoModel>(json);

            Assert.IsTrue(expectedSortedPropertyNames.SequenceEqual(actualSortedPropertyNames));
            Assert.AreEqual(1010000u, model.version);
            Assert.AreEqual(70012u, model.protocolversion);
            Assert.AreEqual(Money.Satoshis(2).ToUnit(MoneyUnit.BTC), model.balance);
            Assert.AreEqual(460828, model.blocks);
            Assert.AreEqual(0, model.timeoffset);
            Assert.AreEqual(44, model.connections);
            Assert.AreEqual(string.Empty, model.proxy);
            Assert.AreEqual(499635929816.6675, model.difficulty, 3);
            Assert.IsFalse(model.testnet);
            Assert.AreEqual(1437418454, model.keypoololdest);
            Assert.AreEqual(101, model.keypoolsize);
            Assert.AreEqual(0u, model.unlocked_until);
            Assert.AreEqual(Money.Satoshis(10000).ToUnit(MoneyUnit.BTC), model.paytxfee);
            Assert.AreEqual(Money.Satoshis(1000).ToUnit(MoneyUnit.BTC), model.relayfee);
            Assert.AreEqual("URGENT: Alert key compromised, upgrade required", model.errors);
        }       
    }
}
