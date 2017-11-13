using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Xunit;
using System.IO;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class APITests:IDisposable
    {
        static HttpClient client = null;

        public void Dispose()
        {
            // This is needed here because of the fact that the Stratis network, when initialized, sets the 
            // Transaction.TimeStamp value to 'true' (look in Network.InitStratisTest() and Network.InitStratisMain()) in order
            // for proof-of-stake to work.
            // Now, there are a few tests where we're trying to parse Bitcoin transaction, but since the TimeStamp is set the true,
            // the execution path is different and the bitcoin transaction tests are failing.
            // Here we're resetting the TimeStamp after every test so it doesn't cause any trouble.

            Transaction.TimeStamp = false;
            Block.BlockSignature = false;

            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }

        /// <summary>
        /// Tests whether the Wallet API method "general-info" can be called and returns a non-empty JSON-formatted string result.
        /// </summary>
        [Fact]
        public void CanGetGeneralInfoViaAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                try
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

                    this.InitializeTestWallet(nodeA);
                    builder.StartAll();

                    var fullNode = nodeA.FullNode;
                    var ApiURI = fullNode.Settings.ApiUri;

                    using (client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = client.GetStringAsync(ApiURI + "api/wallet/general-info?name=test").GetAwaiter().GetResult();

                        Assert.StartsWith("{\"walletFilePath\":null,\"network\":\"RegTest\",\"creationTime\":\"", response);
                    }
                }
                finally
                {
                    this.Dispose();
                }
            }
        }

        /// <summary>
        /// Tests whether the Miner API method "startstaking" can be called.
        /// </summary>
        [Fact]
        public void CanStartStakingViaAPI()
        {
            try
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

                    using (client = new HttpClient())
                    {
                        WalletManager walletManager = fullNode.NodeService<IWalletManager>() as WalletManager;

                        // create the wallet
                        var model = new StartStakingRequest { Name = "apitest", Password = "123456" };
                        var mnemonic = walletManager.CreateWallet(model.Password, model.Name);

                        var content = new StringContent(model.ToString(), Encoding.UTF8, "application/json");
                        var response = client.PostAsync(ApiURI + "api/miner/startstaking", content).GetAwaiter().GetResult();
                        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

                        var responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        Assert.Equal("", responseText);

                        MiningRPCController controller = fullNode.NodeService<MiningRPCController>();
                        GetStakingInfoModel info = controller.GetStakingInfo();

                        Assert.NotNull(info);
                        Assert.True(info.Enabled);
                        Assert.False(info.Staking);
                    }
                }
            }
            finally
            {
                this.Dispose();
            }
        }

        /// <summary>
        /// Tests whether the RPC API method "callbyname" can be called and returns a non-empty JSON formatted result.
        /// </summary>
        [Fact]
        public void CanCallRPCMethodViaRPCsCallByNameAPI()
        {
            try
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

                    using (client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = client.GetStringAsync(ApiURI + "api/rpc/callbyname?methodName=getblockhash&height=0").GetAwaiter().GetResult();

                        Assert.Equal("\"0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206\"", response);
                    }
                }
            }
            finally
            {
                this.Dispose();
            }
        }

        /// <summary>
        /// Tests whether the RPC API method "listmethods" can be called and returns a JSON formatted list of strings.
        /// </summary>
        [Fact]
        public void CanListRPCMethodsViaRPCsListMethodsAPI()
        {
            try
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

                    using (client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var response = client.GetStringAsync(ApiURI + "api/rpc/listmethods").GetAwaiter().GetResult();

                        Assert.StartsWith("[{\"", response);
                    }
                }
            }
            finally
            {
                this.Dispose();
            }
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isnt' already present.
        /// </summary>
        /// <param name="node">Core node for the test.</param>
        private void InitializeTestWallet(CoreNode node)
        {
            string testWalletPath = Path.Combine(node.DataFolder, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}
