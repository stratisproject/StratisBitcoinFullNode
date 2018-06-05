using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;
using Xunit;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Api;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;

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
        private readonly TestAssets testAssets;

        public Sidechain_Api_Shall()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            testAssets = new TestAssets();
        }

        [Fact]
        public async Task read_the_sidechain_list()
        {
            Action<IFullNodeBuilder> buildCallBack = (n) => n.UseSidechains().UseApi().MockIBD();
            using (var nodeBuilder = NodeBuilder.Create())
            {
                var node = nodeBuilder.CreatePosSidechainNode("enigma", true, buildCallBack);

                var sidechains = await this.ListSidechainsAsync(node.ApiPort);
                sidechains.ContainsKey("enigma").Should().BeTrue();
                sidechains["enigma"].Should().NotBeNull();
            }
        }

        [Fact]
        public async Task add_a_new_sidechain()
        {
            Action<IFullNodeBuilder> buildCallBack = (n) => n.UseSidechains().UseApi().MockIBD();
            using (var nodeBuilder = NodeBuilder.Create())
            {
                var node = nodeBuilder.CreatePosSidechainNode("enigma", true, buildCallBack);

                var sidechainInfo = this.testAssets.GetSidechainInfoRequest("sidey12", 3);
                await this.AddSidechainAsync(node.ApiPort, sidechainInfo);
            }
        }

        [Fact]
        public async Task add_a_new_sidechain_should_be_able_to_retrieve_the_chain_after_creating_it()
        {
            Action<IFullNodeBuilder> buildCallBack = (n) => n.UseSidechains().UseApi().MockIBD();
            using (var nodeBuilder = NodeBuilder.Create())
            {
                var newChainName = "random";
                var node = nodeBuilder.CreatePosSidechainNode("enigma", true, buildCallBack);

                var sidechains = await this.ListSidechainsAsync(node.ApiPort);
                sidechains.Should().NotContainKey(newChainName); //just to make sure we actually create it
                
                var newSidechainInfo = this.testAssets.GetSidechainInfoRequest(newChainName, 9);
                await this.AddSidechainAsync(node.ApiPort, newSidechainInfo);
                
                sidechains = await ListSidechainsAsync(node.ApiPort);
                sidechains.Should().ContainKey(newChainName);
                testAssets.VerifySidechainInfo(sidechains[newChainName], newChainName, 9);
            }
        }

        [Fact]
        public async Task be_able_to_give_CoinDetails()
        {
            Action<IFullNodeBuilder> buildCallBack = (n) => n.UseSidechains().UseApi().MockIBD();
            using (var nodeBuilder = NodeBuilder.Create())
            {
                var node = nodeBuilder.CreatePosSidechainNode("enigma", true, buildCallBack);

                var coinDetails = await this.GetCoinDetailsAsync(node.ApiPort);
                coinDetails.Name.Should().Be("enigmaCoin");
                coinDetails.Symbol.Should().Be("EGA2");
                coinDetails.Type.Should().Be(12345);
            }
        }

        private async Task<Dictionary<string, SidechainInfo>> ListSidechainsAsync(int apiPort)
        {
            var uri = new Uri($"http://localhost:{apiPort}/api/Sidechains/list-sidechains");
            var httpResponseMessage = await this.client.GetAsync(uri);
            httpResponseMessage.IsSuccessStatusCode.Should().BeTrue(httpResponseMessage.ReasonPhrase);
            string json = await httpResponseMessage.Content.ReadAsStringAsync();
            var dictionaryOut = JsonConvert.DeserializeObject<Dictionary<string, SidechainInfo>>(json);
            return dictionaryOut;
        }

        private async Task AddSidechainAsync(int apiPort, SidechainInfoRequest sidechainInfoRequest)
        {
            var uri = new Uri($"http://localhost:{apiPort}/api/Sidechains/new-sidechain");
            var content = new JsonContent(sidechainInfoRequest);
            var httpResponseMessage = await this.client.PostAsync(uri, content);
            httpResponseMessage.IsSuccessStatusCode.Should().BeTrue();
        }

        private async Task<CoinDetails> GetCoinDetailsAsync(int apiPort)
        {

            var uri = new Uri($"http://localhost:{apiPort}/api/Sidechains/get-coindetails");
            var httpResponseMessage = await client.GetAsync(uri);
            httpResponseMessage.IsSuccessStatusCode.Should().BeTrue(httpResponseMessage.ReasonPhrase);
            var coinDetails = JsonConvert.DeserializeObject<CoinDetails>(await httpResponseMessage.Content.ReadAsStringAsync());
            return coinDetails;
        }

        public void Dispose()
        {
            if (client != null) client.Dispose();
        }
    }
}
