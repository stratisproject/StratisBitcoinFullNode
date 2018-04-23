using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using FluentAssertions;
using NBitcoin;
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
    public partial class ApiSpecification : BddSpecification
    {
        private HttpClient httpClient = null;
        private ApiTestsFixture apiTestsFixture;
        private Uri apiUri;
        private string response;
        private FullNode fullNode;
        private StartStakingRequest model;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.apiTestsFixture = new ApiTestsFixture();

            // These tests use Network.Stratis.
            // Ensure that these static flags have the expected value.
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;

            this.httpClient = new HttpClient();
        }

        protected override void AfterTest()
        {
            // This is needed here because of the fact that the Stratis network, when initialized, sets the
            // Transaction.TimeStamp value to 'true' (look in Network.InitStratisTest() and Network.InitStratisMain()) in order
            // for proof-of-stake to work.
            // Now, there are a few tests where we're trying to parse Bitcoin transaction, but since the TimeStamp is set the true,
            // the execution path is different and the bitcoin transaction tests are failing.
            // Here we're resetting the TimeStamp after every test so it doesn't cause any trouble.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;

            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            this.apiTestsFixture.Dispose();
        }

        private void a_proof_of_work_node_api()
        {
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
            var node = this.apiTestsFixture.stratisPowNode.FullNode;
            this.apiUri = node.NodeService<ApiSettings>().ApiUri;
        }

        private void a_proof_of_stake_node_api()
        {
            this.fullNode = this.apiTestsFixture.stratisStakeNode.FullNode;
            this.apiUri = this.fullNode.NodeService<ApiSettings>().ApiUri;

            Assert.NotNull(this.fullNode.NodeService<IPosMinting>(true));
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
            Assert.StartsWith("{\"walletFilePath\":\"", this.response);
        }

        private void staking_is_started()
        {
            var content = new StringContent(this.model.ToString(), Encoding.UTF8, "application/json");
            var response = this.httpClient.PostAsync(this.apiUri + "api/miner/startstaking", content).Result;
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            var responseText = response.Content.ReadAsStringAsync().Result;
            Assert.Equal(string.Empty, responseText);
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
            this.response = this.httpClient.GetStringAsync(this.apiUri + "api/rpc/listmethods").GetAwaiter().GetResult();
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            MiningRPCController controller = this.fullNode.NodeService<MiningRPCController>();
            GetStakingInfoModel info = controller.GetStakingInfo();
            Assert.NotNull(info);
            Assert.True(info.Enabled);
            Assert.False(info.Staking);
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