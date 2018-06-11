using FluentAssertions;
using Newtonsoft.Json;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedSidechains.IntegrationTests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration.Network;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests
{
    [Collection("SidechainIdentifierTests")]
    public class Sidechain_Api_Shall : IDisposable
    {
        public class JsonContent : StringContent
        {
            public JsonContent(object obj) :
                base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
            {

            }
        }

        private readonly HttpClient client;
        public Sidechain_Api_Shall()
        {
            client = new HttpClient();
            //client.DefaultRequestHeaders.Accept.Clear();
            //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }


        //[Fact(Skip = "Figure out why the constructor of SidechainsManager is called twice")]
        [Fact]
        //TODO Figure out why the constructor of SidechainsManager is called twice,
        //once with good nodeSettings, and once with empty nodeSettings, causing this to fail
        public async Task be_able_to_give_CoinDetails()
        {
            using (var nodeBuilder = NodeBuilder.Create(this))
            {
                var node = nodeBuilder.CreatePowPosSidechainApiMiningNode(SidechainNetwork.SidechainRegTest, start: true);
                var coinDetails = await this.GetCoinDetailsAsync(node.ApiPort());
                coinDetails.Name.Should().Be("TestApex");
                coinDetails.Symbol.Should().Be("TAPEX");
                coinDetails.Type.Should().Be(3001);
            }
        }

        private async Task<CoinDetails> GetCoinDetailsAsync(int apiPort)
        {

            var uri = new Uri($"http://localhost:{apiPort}/api/Sidechains/get-coindetails");
            var httpResponseMessage = await client.GetAsync(uri);
            httpResponseMessage.IsSuccessStatusCode.Should().BeTrue(httpResponseMessage.ReasonPhrase);
            var jsonResult = await httpResponseMessage.Content.ReadAsStringAsync();
            var coinDetails = JsonConvert.DeserializeObject<CoinDetails>(jsonResult);
            return coinDetails;
        }

        public void Dispose()
        {
            if (client != null) client.Dispose();
        }
    }
}
