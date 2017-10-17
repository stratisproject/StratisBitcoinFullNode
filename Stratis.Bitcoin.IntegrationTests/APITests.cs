using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Api;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class APITests
    {
        static HttpClient client = new HttpClient();

        /// <summary>
        /// Tests whether the Wallet API method "general-info" can be called and returns a non-empty JSON-formatted string result.
        /// </summary>
        [Fact]
        public void CanGetGeneralInfoViaAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                   .UseConsensus()
                   .UseBlockStore()
                   .UseMempool()
                   .AddMining()
                   .UseWallet()
                   .UseApi()
                   .AddRPC();
                });

                builder.StartAll();

                var fullNode = nodeA.FullNode;
                var ApiURI = fullNode.Settings.ApiUri;

                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = client.GetStringAsync(ApiURI + "api/wallet/general-info?name=test").GetAwaiter().GetResult();

                    Assert.True(response.StartsWith("{\"walletFilePath\":null,\"network\":\"RegTest\",\"creationTime\":\""));
                }
            }
        }

        /// <summary>
        /// Tests whether the Miner API method "startstaking" can be called.
        /// </summary>
        [Fact]
        public void CanStartStakingViaAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPosNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                   .UseStratisConsensus()
                   .UseBlockStore()
                   .UseMempool()
                   .UseWallet()
                   .AddPowPosMining()
                   .UseApi()
                   .AddRPC();
                });

                builder.StartAll();

                var fullNode = nodeA.FullNode;
                var ApiURI = fullNode.Settings.ApiUri;

                Assert.NotNull(fullNode.NodeService<PosMinting>(true));

                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    WalletManager walletManager = fullNode.NodeService<IWalletManager>() as WalletManager;

                    // create the wallet
                    var model = new StartStakingRequest() { Name = "apitest", Password = "123456" };
                    var mnemonic = walletManager.CreateWallet(model.Password, model.Name);

                    var content = new StringContent(model.ToString(), Encoding.UTF8, "application/json");
                    var response = client.PostAsync(ApiURI + "api/miner/startstaking", content).GetAwaiter().GetResult();
                    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

                    var responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Assert.Equal("", responseText);

                    MiningRPCController controller = fullNode.NodeService<MiningRPCController>();
                    GetStakingInfoModel info = controller.GetStakingInfo();

                    Assert.NotNull(info);
                    Assert.Equal(true, info.Enabled);
                    Assert.Equal(false, info.Staking);
                }
            }
        }

        /// <summary>
        /// Tests whether the Wallet API method "callbyname" can be called and returns a non-empty JSON formatted result.
        /// </summary>
        [Fact]
        public void CanCallRPCMethodViaRPCsCallByNameAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                CoreNode nodeA = builder.CreateStratisPowNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                   .UseConsensus()
                   .UseBlockStore()
                   .UseMempool()
                   .AddMining()
                   .UseWallet()
                   .UseApi()
                   .AddRPC();
                });

                builder.StartAll();

                var fullNode = nodeA.FullNode;
                var ApiURI = fullNode.Settings.ApiUri;

                using (Node nodeB = nodeA.CreateNodeClient())
                {
                    nodeB.VersionHandshake();

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = client.GetStringAsync(ApiURI + "api/rpc/callbyname?methodName=getblockhash&height=0").GetAwaiter().GetResult();

                    Assert.Equal("\"0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206\"", response);
                }
            }
        }
    }
}
