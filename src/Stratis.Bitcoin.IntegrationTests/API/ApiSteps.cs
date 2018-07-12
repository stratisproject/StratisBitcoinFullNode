using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private const string PosNode = "pos_node";
        private const string WalletOne = "wallet_one";
        private const string WalletTwo = "wallet_two";
        private const string AccountZero = "account 0";
        private const string AccountOne = "account 1";
        private const string WalletPassword = "wallet_password";
        private const string StratisRegTest = "StratisRegTest";

        private HttpClient httpClient;
        private Uri apiUri;
        private string response;
        private NodeGroupBuilder nodeGroupBuilder;
        private IDictionary<string, CoreNode> nodes;
        private HttpResponseMessage postResponse;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.httpClient = new HttpClient();
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));

            this.nodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            this.nodeGroupBuilder.Dispose();
        }

        private void a_proof_of_stake_node_with_api_enabled()
        {
            this.nodes = this.nodeGroupBuilder.CreateStratisPosApiNode(PosNode)
                .Start()
                .Build();

            this.nodes[PosNode].FullNode.NodeService<IPosMinting>(true)
                .Should().NotBeNull();

            this.apiUri = this.nodes[PosNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void getting_general_info()
        {
            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet("password", "test_general_info_wallet");
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/wallet/general-info?name=test_general_info_wallet").GetAwaiter().GetResult();
        }

        private void general_information_about_the_wallet_and_node_is_returned()
        {
            var generalInfoResponse = JsonDataSerializer.Instance.Deserialize<WalletGeneralInfoModel>(this.response);

            generalInfoResponse.WalletFilePath.Should().ContainAll(StratisRegTest, "test_general_info_wallet.wallet.json");
            generalInfoResponse.Network.Name.Should().Be(StratisRegTest);
            generalInfoResponse.ChainTip.Should().Be(0);
            generalInfoResponse.IsChainSynced.Should().BeFalse();
            generalInfoResponse.ConnectedNodes.Should().Be(0);
            generalInfoResponse.IsDecrypted.Should().BeTrue();
        }

        private void staking_is_started()
        {
            var stakingRequest = new StartStakingRequest() { Name = "test_general_info_wallet", Password = "password" };

            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet("password", "test_general_info_wallet");

            var httpRequestContent = new StringContent(stakingRequest.ToString(), Encoding.UTF8, JsonContentType);
            HttpResponseMessage stakingResponse = this.httpClient.PostAsync($"{this.apiUri}api/miner/startstaking", httpRequestContent).GetAwaiter().GetResult();

            stakingResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            string responseText = stakingResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            responseText.Should().BeEmpty();
        }

        private void calling_rpc_getblockhash_via_callbyname()
        {
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/rpc/callbyname?methodName=getblockhash&height=0")
                .GetAwaiter().GetResult();
        }

        private void calling_rpc_listmethods()
        {
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/rpc/listmethods").GetAwaiter().GetResult();
        }

        private void calling_recover_via_extpubkey_for_account_0()
        {
            this.RecoverViaExtPubKey(WalletOne, "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1", 0);
        }

        private void attempting_to_add_an_account()
        {
            var request = new GetUnusedAccountModel()
            {
                WalletName = WalletOne,
                Password = WalletPassword
            };

            this.postResponse = this.httpClient.PostAsJsonAsync($"{this.apiUri}api/Wallet/account", request)
                .GetAwaiter().GetResult();
        }

        private void an_extpubkey_only_wallet_with_account_0()
        {
            this.RecoverViaExtPubKey(WalletOne, "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1", 0);
        }

        private void calling_recover_via_extpubkey_for_account_1()
        {
            //NOTE: use legacy stratis xpub key format for this one to ensure that works too.
            this.RecoverViaExtPubKey(WalletTwo, "xq5hcJV8uJDLaNytrg6FphHY1vdqxP1rCPhAmp4xZwpxzYyYEscYEujAmNR5NrPfy9vzQ6BajEqtFezcyRe4zcGHH3dR6BKaKov43JHd8UYhBVy", 1);
        }

        private void RecoverViaExtPubKey(string walletName, string extPubKey, int accountIndex)
        {
            var request = new WalletExtPubRecoveryRequest
            {
                ExtPubKey = extPubKey,
                AccountIndex = accountIndex,
                Name = walletName
            };

            this.postResponse = this.httpClient.PostAsJsonAsync($"{this.apiUri}api/Wallet/recover-via-extpubkey", request)
                .GetAwaiter().GetResult();

            this.postResponse.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        private void a_wallet_is_created_without_private_key_for_account_0()
        {
            this.CheckAccountExists(WalletOne, 0);
        }

        private void a_wallet_is_created_without_private_key_for_account_1()
        {
            this.CheckAccountExists(WalletTwo, 1);
        }

        private void CheckAccountExists(string walletName, int accountIndex)
        {
            string getBalanceUrl = $"{this.apiUri}api/Wallet/balance?walletname={walletName}&AccountName=account {accountIndex}";

            this.response = this.httpClient
                .GetStringAsync(getBalanceUrl)
                .GetAwaiter().GetResult();

            this.response.Should()
                .Be("{\"balances\":[{\"accountName\":\"account " + accountIndex + "\",\"accountHdPath\":\"m/44'/105'/" + accountIndex + "'\",\"coinType\":105,\"amountConfirmed\":0,\"amountUnconfirmed\":0}]}");
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.nodes[PosNode].FullNode.NodeService<MiningRPCController>();
            GetStakingInfoModel stakingInfo = miningRpcController.GetStakingInfo();

            stakingInfo.Should().NotBeNull();
            stakingInfo.Enabled.Should().BeTrue();
            stakingInfo.Staking.Should().BeFalse();
        }

        private void it_is_rejected_and_user_is_told_to_restore_instead()
        {
            this.postResponse.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        private void the_blockhash_is_returned()
        {
            this.response.Should().Be("\"" + Network.StratisRegTest.Consensus.HashGenesisBlock + "\"");
        }

        private void a_full_list_of_available_commands_is_returned()
        {
            var commands = JsonDataSerializer.Instance.Deserialize<List<RpcCommandModel>>(this.response);

            commands.Count.Should().Be(16);
            commands.Should().Contain(x => x.Command == "stop");
            commands.Should().Contain(x => x.Command == "getrawtransaction <txid> [<verbose>]");
            commands.Should().Contain(x => x.Command == "gettxout <txid> <vout> [<includemempool>]");
            commands.Should().Contain(x => x.Command == "getblockcount");
            commands.Should().Contain(x => x.Command == "getinfo");
            commands.Should().Contain(x => x.Command == "getblockheader <hash> [<isjsonformat>]");
            commands.Should().Contain(x => x.Command == "validateaddress <address>");
            commands.Should().Contain(x => x.Command == "addnode <endpointstr> <command>");
            commands.Should().Contain(x => x.Command == "getpeerinfo");
            commands.Should().Contain(x => x.Command == "getbestblockhash");
            commands.Should().Contain(x => x.Command == "getblockhash <height>");
            commands.Should().Contain(x => x.Command == "getrawmempool");
            commands.Should().Contain(x => x.Command == "generate <blockcount>");
            commands.Should().Contain(x => x.Command == "startstaking <walletname> <walletpassword>");
            commands.Should().Contain(x => x.Command == "getstakinginfo [<isjsonformat>]");
            commands.Should().Contain(x => x.Command == "sendtoaddress <bitcoinaddress> <amount>");
        }
    }
}