using System.Collections.Generic;
using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;
using Xunit;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests
{
    [Collection("SidechainIdentifierTests")]
    public class SidechainInfo_Shall
    {
        TestAssets testAssets = new TestAssets();

        private JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Include,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        [Fact(Skip = "Fails after nuget update. The definition of testAssets needs reviewing.")]
        public void round_trip_json_serialize()
        {
            var sidechainInfoIn = this.testAssets.GetSidechainInfo("enigma", 0);
            string json = JsonConvert.SerializeObject(sidechainInfoIn, Formatting.Indented, this.jsonSerializerSettings);

            var sidechainInfoOut = JsonConvert.DeserializeObject<SidechainInfo>(json);
            this.testAssets.VerifySidechainInfo(sidechainInfoOut, "enigma", 0);
        }

        [Fact(Skip = "Fails after nuget update. The definition of testAssets needs reviewing.")]
        public void round_trip_json_serialize_as_dictionary()
        {
            var sidechainInfo0 = this.testAssets.GetSidechainInfo("enigma", 0);
            var sidechainInfo1 = this.testAssets.GetSidechainInfo("sidey", 12);

            var dictionaryIn = new Dictionary<string, SidechainInfo>();
            dictionaryIn.Add("enigma", sidechainInfo0);
            dictionaryIn.Add("sidey", sidechainInfo1);
            string json = JsonConvert.SerializeObject(dictionaryIn, Formatting.Indented, this.jsonSerializerSettings);

            var dictionaryOut = JsonConvert.DeserializeObject< Dictionary<string, SidechainInfo>>(json);
            this.testAssets.VerifySidechainInfo(dictionaryOut["enigma"], "enigma", 0);
            this.testAssets.VerifySidechainInfo(dictionaryOut["sidey"], "sidey", 12);
        }
    }
}
