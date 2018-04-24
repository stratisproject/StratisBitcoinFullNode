﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.IntegrationTests.TestFramework;
using Xunit.Abstractions;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Builders;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private const string PosNode = "pos_node";
        private const string WalletName = "wallet_name";
        private const string WalletPassword = "wallet_password";
        private const string StratisRegTest = "StratisRegTest";

        private HttpClient httpClient;
        private Uri apiUri;
        private string response;
        private NodeGroupBuilder nodeGroupBuilder;
        private IDictionary<string, CoreNode> nodes;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.httpClient = new HttpClient();
            this.nodeGroupBuilder = new NodeGroupBuilder();
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
            Transaction.TimeStamp = true;
            Block.BlockSignature = true;

            this.nodes = this.nodeGroupBuilder.CreateStratisPosApiNode(PosNode)
                .Start()
                .WithWallet(WalletName, WalletPassword)
                .Build();

            this.nodes[PosNode].FullNode.NodeService<IPosMinting>(true)
                .Should().NotBeNull();

            this.apiUri = this.nodes[PosNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void getting_general_info()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/wallet/general-info?name={WalletName}").GetAwaiter().GetResult();
        }

        private void general_information_about_the_wallet_and_node_is_returned()
        {
            var generalInfoResponse = JsonDataSerializer.Instance.Deserialize<WalletGeneralInfoModel>(this.response);

            generalInfoResponse.WalletFilePath.Should().ContainAll(StratisRegTest, $"{WalletName}.wallet.json");
            generalInfoResponse.Network.Name.Should().Be(StratisRegTest);
            generalInfoResponse.ChainTip.Should().Be(0);
            generalInfoResponse.IsChainSynced.Should().BeFalse();
            generalInfoResponse.ConnectedNodes.Should().Be(0);
            generalInfoResponse.IsDecrypted.Should().BeTrue();
        }

        private void staking_is_started()
        {
            var stakingRequest = new StartStakingRequest() { Name = WalletName, Password = WalletPassword };

            var httpRequestContent = new StringContent(stakingRequest.ToString(), Encoding.UTF8, JsonContentType);
            var stakingResponse = this.httpClient.PostAsync($"{this.apiUri}api/miner/startstaking", httpRequestContent).GetAwaiter().GetResult();

            stakingResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            var responseText = stakingResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            responseText.Should().BeEmpty();
        }

        private void calling_rpc_getblockhash_via_callbyname()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));

            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/rpc/callbyname?methodName=getblockhash&height=0")
                .GetAwaiter().GetResult();
        }

        private void calling_rpc_listmethods()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/rpc/listmethods").GetAwaiter().GetResult();
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.nodes[PosNode].FullNode.NodeService<MiningRPCController>();
            var stakingInfo = miningRpcController.GetStakingInfo();

            stakingInfo.Should().NotBeNull();
            stakingInfo.Enabled.Should().BeTrue();
            stakingInfo.Staking.Should().BeFalse();
        }

        private void the_blockhash_is_returned()
        {
            this.response.Should().Be("\"" + Network.StratisRegTest.Consensus.HashGenesisBlock + "\"");
        }

        private void a_full_list_of_available_commands_is_returned()
        {
            var commands = JsonDataSerializer.Instance.Deserialize<List<RpcCommandModel>>(this.response);

            commands.Count.Should().Be(16);
            commands.Any(x => x.Command == "stop").Should().BeTrue();
            commands.Any(x => x.Command == "getrawtransaction <txid> [<verbose>]").Should().BeTrue();
            commands.Any(x => x.Command == "gettxout <txid> <vout> [<includemempool>]").Should().BeTrue();
            commands.Any(x => x.Command == "getblockcount").Should().BeTrue();
            commands.Any(x => x.Command == "getinfo").Should().BeTrue();
            commands.Any(x => x.Command == "getblockheader <hash> [<isjsonformat>]").Should().BeTrue();
            commands.Any(x => x.Command == "validateaddress <address>").Should().BeTrue();
            commands.Any(x => x.Command == "addnode <endpointstr> <command>").Should().BeTrue();
            commands.Any(x => x.Command == "getpeerinfo").Should().BeTrue();
            commands.Any(x => x.Command == "getbestblockhash").Should().BeTrue();
            commands.Any(x => x.Command == "getblockhash <height>").Should().BeTrue();
            commands.Any(x => x.Command == "getrawmempool").Should().BeTrue();
            commands.Any(x => x.Command == "generate <blockcount>").Should().BeTrue();
            commands.Any(x => x.Command == "startstaking <walletname> <walletpassword>").Should().BeTrue();
            commands.Any(x => x.Command == "getstakinginfo [<isjsonformat>]").Should().BeTrue();
            commands.Any(x => x.Command == "sendtoaddress <bitcoinaddress> <amount>").Should().BeTrue();
        }
    }
}