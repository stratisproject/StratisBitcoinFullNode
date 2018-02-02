using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class APITests : IDisposable, IClassFixture<ApiTestsFixture>
    {
        private static HttpClient client = null;
        private ApiTestsFixture apiTestsFixture;

        public APITests(ApiTestsFixture apiTestsFixture)
        {
            this.apiTestsFixture = apiTestsFixture;

            // These tests use Network.Stratis.
            // Ensure that these static flags have the expected value.
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
        }

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

            Transaction.TimeStamp = false;
            Block.BlockSignature = false;

            try
            {
                var fullNode = this.apiTestsFixture.stratisPowNode.FullNode;
                var apiURI = fullNode.NodeService<ApiSettings>().ApiUri;

                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = client.GetStringAsync(apiURI + "api/wallet/general-info?name=test").GetAwaiter().GetResult();
                    Assert.StartsWith("{\"walletFilePath\":\"", response);
                }
            }
            finally
            {
                this.Dispose();
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
                var fullNode = this.apiTestsFixture.stratisStakeNode.FullNode;
                var apiURI = fullNode.NodeService<ApiSettings>().ApiUri;

                Assert.NotNull(fullNode.NodeService<IPosMinting>(true));

                using (client = new HttpClient())
                {
                    WalletManager walletManager = fullNode.NodeService<IWalletManager>() as WalletManager;

                    // create the wallet
                    var model = new StartStakingRequest { Name = "apitest", Password = "123456" };
                    var mnemonic = walletManager.CreateWallet(model.Password, model.Name);

                    var content = new StringContent(model.ToString(), Encoding.UTF8, "application/json");
                    var response = client.PostAsync(apiURI + "api/miner/startstaking", content).GetAwaiter().GetResult();
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
                var fullNode = this.apiTestsFixture.stratisPowNode.FullNode;
                var apiURI = fullNode.NodeService<ApiSettings>().ApiUri;

                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = client.GetStringAsync(apiURI + "api/rpc/callbyname?methodName=getblockhash&height=0").GetAwaiter().GetResult();

                    Assert.Equal("\"0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206\"", response);
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
                var fullNode = this.apiTestsFixture.stratisPowNode.FullNode;
                var apiURI = fullNode.NodeService<ApiSettings>().ApiUri;

                using (client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = client.GetStringAsync(apiURI + "api/rpc/listmethods").GetAwaiter().GetResult();

                    Assert.StartsWith("[{\"", response);
                }
            }

            finally
            {
                this.Dispose();
            }
        }
    }

    public class ApiTestsFixture : IDisposable
    {
        public NodeBuilder builder;
        public CoreNode stratisPowNode;
        public CoreNode stratisStakeNode;
        private bool initialBlockSignature;

        public ApiTestsFixture()
        {
            this.initialBlockSignature = Block.BlockSignature;
            Block.BlockSignature = false;

            this.builder = NodeBuilder.Create();

            this.stratisPowNode = this.builder.CreateStratisPowNode(false, fullNodeBuilder =>
            {
                fullNodeBuilder
               .UsePowConsensus()
               .UseBlockStore()
               .UseMempool()
               .AddMining()
               .UseWallet()
               .UseApi()
               .AddRPC();
            });

            // start api on different ports
            this.stratisPowNode.ConfigParameters.Add("apiuri", "http://localhost:37221");
            this.builder.StartAll();

            // Move a wallet file to the right folder and restart the wallet manager to take it into account.
            this.InitializeTestWallet(this.stratisPowNode.FullNode.DataFolder.WalletPath);
            var walletManager = this.stratisPowNode.FullNode.NodeService<IWalletManager>() as WalletManager;
            walletManager.Start();

            Block.BlockSignature = true;

            this.stratisStakeNode = this.builder.CreateStratisPosNode(false, fullNodeBuilder =>
            {
                fullNodeBuilder
                .UsePosConsensus()
                .UseBlockStore()
                .UseMempool()
                .UseWallet()
                .AddPowPosMining()
                .UseApi()
                .AddRPC();
            });

            this.stratisStakeNode.ConfigParameters.Add("apiuri", "http://localhost:37222");

            this.builder.StartAll();
        }

        // note: do not call this dispose in the class itself xunit will handle it.
        public void Dispose()
        {
            this.builder.Dispose();
            Block.BlockSignature = this.initialBlockSignature;
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isnt' already present.
        /// </summary>
        /// <param name="path">The path of the folder to move the wallet to.</param>
        public void InitializeTestWallet(string path)
        {
            string testWalletPath = Path.Combine(path, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}