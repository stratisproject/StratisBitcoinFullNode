using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Api
{
    public class APITests : IDisposable
    {
        private bool initialBlockSignature;
        private bool initialTimeStamp;

        public APITests()
        {
            this.initialBlockSignature = Transaction.TimeStamp;
            this.initialTimeStamp = Block.BlockSignature;

            Transaction.TimeStamp = true;
            Block.BlockSignature = true;
        }

        public void Dispose()
        {
            Transaction.TimeStamp = this.initialBlockSignature;
            Block.BlockSignature = this.initialBlockSignature;
        }

        /// <summary>
        /// Tests whether the Wallet API method "general-info" can be called and returns a non-empty JSON-formatted string result.
        /// </summary>
        [Fact]
        public void CanGetGeneralInfoViaAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var posNode = builder.CreateStratisPosApiNode();
                posNode.Start();

                var walletManager = posNode.FullNode.NodeService<IWalletManager>() as WalletManager;
                walletManager.CreateWallet("testapi", "testapi");

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var apiURI = posNode.FullNode.NodeService<ApiSettings>().ApiUri;
                    var walletUrl = apiURI + "api/wallet/general-info?name=testapi";
                    var response = httpClient.GetStringAsync(walletUrl).GetAwaiter().GetResult();
                    Assert.StartsWith("{\"walletFilePath\":\"", response);
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
                var posNode = builder.CreateStratisPosApiNode();
                posNode.Start();

                var apiURI = posNode.FullNode.NodeService<ApiSettings>().ApiUri;

                using (var httpClient = new HttpClient())
                {
                    var walletManager = posNode.FullNode.NodeService<IWalletManager>() as WalletManager;

                    // create the wallet
                    var model = new StartStakingRequest { Name = "apitest", Password = "123456" };
                    var mnemonic = walletManager.CreateWallet(model.Password, model.Name);

                    var content = new StringContent(model.ToString(), Encoding.UTF8, "application/json");
                    var response = httpClient.PostAsync(apiURI + "api/miner/startstaking", content).GetAwaiter().GetResult();
                    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

                    var responseText = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Assert.Equal("", responseText);

                    MiningRPCController controller = posNode.FullNode.NodeService<MiningRPCController>();
                    GetStakingInfoModel info = controller.GetStakingInfo();

                    Assert.NotNull(info);
                    Assert.True(info.Enabled);
                    Assert.False(info.Staking);
                }
            }
        }

        /// <summary>
        /// Tests whether the RPC API method "callbyname" can be called and returns a non-empty JSON formatted result.
        /// </summary>
        [Fact]
        public void CanCallRPCMethodViaRPCsCallByNameAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var posNode = builder.CreateStratisPosApiNode();
                posNode.Start();

                var apiURI = posNode.FullNode.NodeService<ApiSettings>().ApiUri;

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = httpClient.GetStringAsync(apiURI + "api/rpc/callbyname?methodName=getblockhash&height=0").GetAwaiter().GetResult();
                    Assert.Equal("\"93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f\"", response);
                }
            }
        }

        /// <summary>
        /// Tests whether the RPC API method "listmethods" can be called and returns a JSON formatted list of strings.
        /// </summary>
        [Fact]
        public void CanListRPCMethodsViaRPCsListMethodsAPI()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var posNode = builder.CreateStratisPosApiNode();
                posNode.Start();

                var apiURI = posNode.FullNode.NodeService<ApiSettings>().ApiUri;

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = httpClient.GetStringAsync(apiURI + "api/rpc/listmethods").GetAwaiter().GetResult();
                    Assert.StartsWith("[{\"", response);
                }
            }
        }
    }
}