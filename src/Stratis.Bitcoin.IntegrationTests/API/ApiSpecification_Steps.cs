using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification, IClassFixture<ApiTestsFixture>
    {
        private HttpClient httpClient;
        private readonly ApiTestsFixture apiTestsFixture;
        private Uri apiUri;
        private string response;
        private FullNode fullNode;
        private StartStakingRequest model;

        public ApiSpecification(ITestOutputHelper output, ApiTestsFixture apiTestsFixture) : base(output)
        {
            this.apiTestsFixture = apiTestsFixture;
        }

        protected override void BeforeTest()
        {
            this.httpClient = new HttpClient();
        }

        protected override void AfterTest()
        {
            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }
        }

        private void a_proof_of_work_node_api()
        {
            var node = this.apiTestsFixture.stratisPowNode.FullNode;
            this.apiUri = node.NodeService<ApiSettings>().ApiUri;
        }

        private void a_proof_of_stake_node_api()
        {
            this.fullNode = this.apiTestsFixture.stratisStakeNode.FullNode;
            this.apiUri = this.fullNode.NodeService<ApiSettings>().ApiUri;

            this.fullNode.NodeService<IPosMinting>(true).Should().NotBeNull();
        }

        private void a_wallet()
        {
            var walletManager = this.fullNode.NodeService<IWalletManager>();
            this.model = new StartStakingRequest { Name = "apitest", Password = "123456" };
            walletManager.CreateWallet(this.model.Password, this.model.Name);
        }

        private void getting_general_info()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/wallet/general-info?name=test").Result;
        }

        private void data_starting_with_wallet_file_path_is_returned()
        {
            // TODO BEFORE PR APPROVAL: improve this check - change method name to information_about_the_wallet_is_returned and implement
            this.response.Should().StartWith("{\"walletFilePath\":\"");
        }

        private void staking_is_started()
        {
            var content = new StringContent(this.model.ToString(), Encoding.UTF8, "application/json");
            var response = this.httpClient.PostAsync(this.apiUri + "api/miner/startstaking", content).Result;

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

            var responseText = response.Content.ReadAsStringAsync().Result;

            responseText.Should().BeEmpty();
        }

        private void calling_rpc_getblockhash_via_callbyname()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/rpc/callbyname?methodName=getblockhash&height=0")
                .Result;
        }

        private void calling_rpc_listmethods()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/rpc/listmethods").Result;
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.fullNode.NodeService<MiningRPCController>();
            var stakingInfo = miningRpcController.GetStakingInfo();

            stakingInfo.Should().NotBeNull();
            stakingInfo.Enabled.Should().BeTrue();
            stakingInfo.Staking.Should().BeFalse();
        }

        private void the_blockhash_is_returned()
        {
            this.response.Should().Be("\"0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206\"");
        }

        private void non_empty_list_returned()
        {
            // TODO BEFORE PR APPROVAL: improve this check  - change method name to the_available_rpc_methods_are_listed and implement
            this.response.Should().StartWith("[{\"");
        }
    }
}