using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private HttpClient httpClient;
        private Uri apiUri;
        private string response;
        private NodeBuilder nodeBuilder;
        private NodeGroupBuilder nodeGroupBuilder;
        private CoreNode posApiNode;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.httpClient = new HttpClient();
            this.nodeBuilder = NodeBuilder.Create();
            this.nodeGroupBuilder = new NodeGroupBuilder();
        }

        protected override void AfterTest()
        {
            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            this.nodeBuilder.Dispose();
            this.nodeGroupBuilder.Dispose();
        }

        private void a_proof_of_stake_node_api()
        {
            // TODO BEFORE PR APPROVAL: Perhaps the nodegroup builder below instead
            //this.nodeGroupBuilder.CreateStratisPosApiNode("testapi").Start()
            //    .WithWallet("testapi", "testapi").Build();

            this.posApiNode = this.nodeBuilder.CreateStratisPosApiNode(start: true);
            this.posApiNode.FullNode.NodeService<IPosMinting>(true).Should().NotBeNull();
            this.apiUri = this.posApiNode.FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void a_wallet()
        {
            var walletManager = this.posApiNode.FullNode.NodeService<IWalletManager>() as WalletManager;
            walletManager.CreateWallet("testapi", "testapi");
        }

        private void getting_general_info()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/wallet/general-info?name=testapi").GetAwaiter().GetResult();
        }

        private void data_starting_with_wallet_file_path_is_returned()
        {
            // TODO BEFORE PR APPROVAL: improve this check - change method name to information_about_the_wallet_is_returned and implement
            this.response.Should().StartWith("{\"walletFilePath\":\"");
        }

        private void staking_is_started()
        {
            var stakingRequest = new StartStakingRequest() { Name = "testapi", Password = "testapi"};

            var content = new StringContent(stakingRequest.ToString(), Encoding.UTF8, JsonContentType);
            var stakingResponse = this.httpClient.PostAsync(this.apiUri + "api/miner/startstaking", content).GetAwaiter().GetResult();

            stakingResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

            var responseText = stakingResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            responseText.Should().BeEmpty();
        }

        private void calling_rpc_getblockhash_via_callbyname()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));

            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/rpc/callbyname?methodName=getblockhash&height=0")
                .GetAwaiter().GetResult();
        }

        private void calling_rpc_listmethods()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/rpc/listmethods").GetAwaiter().GetResult();
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.posApiNode.FullNode.NodeService<MiningRPCController>();
            var stakingInfo = miningRpcController.GetStakingInfo();

            stakingInfo.Should().NotBeNull();
            stakingInfo.Enabled.Should().BeTrue();
            stakingInfo.Staking.Should().BeFalse();
        }

        private void the_blockhash_is_returned()
        {
            this.response.Should().Be("\"93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f\"");
        }

        private void non_empty_list_returned()
        {
            // TODO BEFORE PR APPROVAL: improve this check  - change method name to the_available_rpc_methods_are_listed and implement
            this.response.Should().StartWith("[{\"");
        }
    }
}