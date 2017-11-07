using System.Linq;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.RPC.Models;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Models
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
        public void GetInfoSerializeFullTest()
        {
            var expectedOrderedPropertyNames = AllPropertyNames;
            var info = new GetInfoModel
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
            Assert.True(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name);

            Assert.Equal(expectedOrderedPropertyNames, actualOrderedPropertyNames);
        }

        [Fact]
        public void GetInfoSerializeSparseTest()
        {
            var expectedOrderedPropertyNames = RequiredPropertyNames;
            var info = new GetInfoModel();

            JObject obj = ModelToJObject(info);
            Assert.True(obj.HasValues);
            var actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name);

            Assert.Equal(expectedOrderedPropertyNames, actualOrderedPropertyNames);
        }

        [Fact]
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

            Assert.Equal(expectedSortedPropertyNames, actualSortedPropertyNames);

        }

        [Fact]
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

            Assert.Equal(expectedSortedPropertyNames, actualSortedPropertyNames);
            Assert.Equal(1010000u, model.version);
            Assert.Equal(70012u, model.protocolversion);
            Assert.Equal(Money.Satoshis(2).ToUnit(MoneyUnit.BTC), model.balance);
            Assert.Equal(460828, model.blocks);
            Assert.Equal(0, model.timeoffset);
            Assert.Equal(44, model.connections);
            Assert.Empty(model.proxy);
            Assert.Equal(499635929816.6675, model.difficulty, 3);
            Assert.False(model.testnet);
            Assert.Equal(1437418454, model.keypoololdest);
            Assert.Equal(101, model.keypoolsize);
            Assert.Equal(0u, model.unlocked_until);
            Assert.Equal(Money.Satoshis(10000).ToUnit(MoneyUnit.BTC), model.paytxfee);
            Assert.Equal(Money.Satoshis(1000).ToUnit(MoneyUnit.BTC), model.relayfee);
            Assert.Equal("URGENT: Alert key compromised, upgrade required", model.errors);
        }

    }
}
