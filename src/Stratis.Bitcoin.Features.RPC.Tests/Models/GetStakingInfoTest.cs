using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.Miner.Models;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Models
{
    /// <summary>
    /// Tests of <see cref="GetStakingInfoModel"/> class.
    /// </summary>
    public class GetStakingInfoModelTest : BaseRPCModelTest
    {
        /// <summary>List of all model properties.</summary>
        private static readonly string[] ModelPropertyNames = new string[] 
        {
            "enabled",
            "staking",
            "errors",
            "currentblocksize",
            "currentblocktx",
            "pooledtx",
            "difficulty",
            "search-interval",
            "weight",
            "netstakeweight",
            "expectedtime",
        };

        /// <summary>
        /// Checks if an instance of a model contains expected values after it is serialized to JSON.
        /// </summary>
        [Fact]
        public void GetStakingInfoSerializeTest()
        {
            IOrderedEnumerable<string> expectedSortedPropertyNames = ModelPropertyNames.OrderBy(name => name);
            var model = new GetStakingInfoModel();

            JObject obj = ModelToJObject(model);
            Assert.True(obj.HasValues);
            IEnumerable<string> actualOrderedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).OrderBy(name => name);

            Assert.Equal(expectedSortedPropertyNames, actualOrderedPropertyNames);
        }

        /// <summary>
        /// Checks if the model is deserialized correctly from a string.
        /// </summary>
        [Fact]
        public void GetStakingInfoDeserializeTest()
        {
            IOrderedEnumerable<string> expectedSortedPropertyNames = ModelPropertyNames.OrderBy(name => name);
            string json = "{\n"
                + "  \"enabled\": true,\n"
                + "  \"staking\": true,\n"
                + "  \"errors\": \"Block rejected by peers\",\n"
                + "  \"currentblocksize\": 151,\n"
                + "  \"currentblocktx\": 1,\n"
                + "  \"pooledtx\": 120,\n"
                + "  \"difficulty\": 77856.9675875571,\n"
                + "  \"search-interval\": 16,\n"
                + "  \"weight\": 98076279000000,\n"
                + "  \"netstakeweight\": 101187415332927,\n"
                + "  \"expectedtime\": 66\n"
                + "}\n";

            JObject obj = JObject.Parse(json);
            IOrderedEnumerable<string> actualSortedPropertyNames = obj.Children().Select(o => (o as JProperty)?.Name).OrderBy(name => name);
            GetStakingInfoModel model = Newtonsoft.Json.JsonConvert.DeserializeObject<GetStakingInfoModel>(json);

            Assert.Equal(expectedSortedPropertyNames, actualSortedPropertyNames);
            Assert.True(model.Enabled);
            Assert.True(model.Staking);
            Assert.Equal("Block rejected by peers", model.Errors);
            Assert.Equal(151, model.CurrentBlockSize);
            Assert.Equal(1, model.CurrentBlockTx);
            Assert.Equal(120, model.PooledTx);
            Assert.Equal(77856.9675875571, model.Difficulty);
            Assert.Equal(16, model.SearchInterval);
            Assert.Equal(98076279000000, model.Weight);
            Assert.Equal(101187415332927, model.NetStakeWeight);
            Assert.Equal(66, model.ExpectedTime);
        }
    }
}
