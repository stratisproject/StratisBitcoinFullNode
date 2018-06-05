using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;
using Newtonsoft.Json;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests
{
    public class Utils
    {
        /// <summary>
        /// This is only meant to be run when a change to the way SidechainInfo class
        /// serialises has happened, in order to get a new vaild content for
        /// the Json file we use in all tests
        /// </summary>
        [Fact]
        private void GenerateJsonSerialisationOfAsset()
        {
            var testAsset = new TestAssets();
            var sidechains = new Dictionary<string, SidechainInfo>() {
                { "enigma", testAsset.GetSidechainInfo("enigma", 0) },
                { "mystery", testAsset.GetSidechainInfo("mystery", 10) },
            };
            var copyPasteThatIntoAsset = JsonConvert.SerializeObject(sidechains, Formatting.Indented);
        }
    }
}
