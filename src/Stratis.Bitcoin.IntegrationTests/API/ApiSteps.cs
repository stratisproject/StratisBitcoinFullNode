using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
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
        private const string SendingNode = "sending_node";
        private const string ReceivingNode = "receiving_node";
        private const string WalletName = "wallet_name";
        private const string ReceivingWalletName = "receiving_wallet_name";
        private const string WalletAccountName = "account 0";
        private const string WalletPassword = "wallet_password";
        private const string StratisRegTest = "StratisRegTest";

        private HdAddress receiverAddress;
        private HttpClient httpClient;
        private HttpResponseMessage response;
        private IDictionary<string, CoreNode> nodes;
        private int maturity;
        private readonly Money transferAmount = Money.COIN * 1;
        private NodeGroupBuilder nodeGroupBuilder;
        private SharedSteps sharedSteps;
        private string responseText;
        private Transaction transaction;
        private uint256 block;
        private Uri apiUri;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
            this.httpClient = new HttpClient();
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
                .WithWallet(WalletName, WalletPassword)
                .Build();

            this.nodes[PosNode].FullNode.NodeService<IPosMinting>(true)
                .Should().NotBeNull();

            this.apiUri = this.nodes[PosNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void sending_node_and_receiving_node_with_api_enabled()
        {
            this.nodes = this.nodeGroupBuilder
                .CreateStratisPowApiNode(SendingNode)
                .Start()
                .NotInIBD()
                .WithWallet(WalletName, WalletPassword)
                .CreateStratisPowApiNode(ReceivingNode)
                .Start()
                .WithWallet(ReceivingWalletName, WalletPassword)
                .NotInIBD()
                .WithConnections()
                .Connect(SendingNode, ReceivingNode)
                .AndNoMoreConnections()
                .Build();

            this.receiverAddress = this.nodes[ReceivingNode].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(ReceivingWalletName, WalletAccountName));

            this.maturity = (int)this.nodes[SendingNode].FullNode
                .Network.Consensus.CoinbaseMaturity;

            this.nodes[SendingNode].SetDummyMinerSecret(new BitcoinSecret(new Key(), this.nodes[SendingNode].FullNode.Network));

            this.apiUri = this.nodes[SendingNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void a_pow_node_with_api_enabled()
        {
            this.nodes = this.nodeGroupBuilder
                .CreateStratisPowApiNode(SendingNode)
                .Start()
                .NotInIBD()
                .WithWallet(WalletName, WalletPassword)
                .Build();

            this.maturity = (int)this.nodes[SendingNode].FullNode
                .Network.Consensus.CoinbaseMaturity;

            this.nodes[SendingNode].SetDummyMinerSecret(new BitcoinSecret(new Key(), this.nodes[SendingNode].FullNode.Network));

            this.apiUri = this.nodes[SendingNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void getting_general_info()
        {
            this.send_api_get_request($"api/wallet/general-info?name={WalletName}");
        }

        private void general_information_about_the_wallet_and_node_is_returned()
        {
            var generalInfoResponse = JsonDataSerializer.Instance.Deserialize<WalletGeneralInfoModel>(this.responseText);

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
            this.response = this.httpClient.PostAsync($"{this.apiUri}api/miner/startstaking", httpRequestContent).GetAwaiter().GetResult();

            this.response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            this.responseText = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            this.responseText.Should().BeEmpty();
        }

        private void calling_rpc_getblockhash_via_callbyname()
        {
            this.send_api_get_request("api/rpc/callbyname?methodName=getblockhash&height=0");
        }

        private void calling_rpc_listmethods()
        {
            this.send_api_get_request("api/rpc/listmethods");
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.nodes[PosNode].FullNode.NodeService<MiningRPCController>();
            GetStakingInfoModel stakingInfo = miningRpcController.GetStakingInfo();

            stakingInfo.Should().NotBeNull();
            stakingInfo.Enabled.Should().BeTrue();
            stakingInfo.Staking.Should().BeFalse();
        }

        private void the_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + Network.StratisRegTest.Consensus.HashGenesisBlock + "\"");
        }

        private void a_full_list_of_available_commands_is_returned()
        {
            var commands = JsonDataSerializer.Instance.Deserialize<List<RpcCommandModel>>(this.responseText);

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

        protected void a_block_is_mined_creating_spendable_coins()
        {
            this.sharedSteps.MineBlocks(1, this.nodes[SendingNode], WalletAccountName, WalletName, WalletPassword);
        }

        private void more_blocks_mined_past_maturity_of_original_block()
        {
            this.sharedSteps.MineBlocks(this.maturity + 10, this.nodes[SendingNode], WalletAccountName, WalletName, WalletPassword);
        }

        private void a_real_transaction()
        {
            IActionResult sendTransactionResult = this.SendTransaction(this.BuildTransaction());
        }

        private IActionResult SendTransaction(IActionResult transactionResult)
        {
            var walletTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;
            if (walletTransactionModel == null)
                return null;
            this.transaction = Transaction.Parse(walletTransactionModel.Hex);
            return this.nodes[SendingNode].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        private IActionResult BuildTransaction()
        {
            IActionResult transactionResult = this.nodes[SendingNode].FullNode.NodeService<WalletController>()
                .BuildTransaction(new BuildTransactionRequest
                {
                    AccountName = WalletAccountName,
                    AllowUnconfirmed = true,
                    Amount = this.transferAmount.ToString(),
                    DestinationAddress = this.receiverAddress.Address,
                    FeeType = FeeType.Medium.ToString("D"),
                    Password = WalletPassword,
                    WalletName = WalletName,
                    FeeAmount = Money.Satoshis(82275).ToString() // Minimum fee
                });
            return transactionResult;
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.block = this.nodes[SendingNode].GenerateStratisWithMiner(1).Single();
            this.nodes[SendingNode].GenerateStratisWithMiner(1);
        }

        private void one_more_block_is_mined_to_get_blockid()
        {
            this.block = this.nodes[SendingNode].GenerateStratisWithMiner(1).Single();
        }

        private void calling_block_with_valid_hash_via_api_returns_block()
        {
            this.send_api_get_request($"api/BlockStore/block?Hash={this.block}&OutputJson=true");
        }

        private void the_real_block_should_be_retrieved()
        {
            JObject.Parse(this.responseText)["hash"].Value<string>()
                .Should().Be(this.block.ToString());
            JObject.Parse(this.responseText)["size"].Value<int>()
                .Should().Be(this.block.Size);
        }

        private void the_block_should_contain_the_transaction()
        {
            JObject.Parse(this.responseText)["transactions"][1].Value<string>()
                .Should().Be(this.transaction.GetHash().ToString());
        }

        private void calling_getblockcount_via_api_returns_an_int()
        {
            this.send_api_get_request("api/BlockStore/getblockcount");
        }

        private void calling_getbestblockhash_via_api()
        {
            this.send_api_get_request("api/Consensus/getbestblockhash");
        }

        private void the_consensus_tip_blockhash_is_returned()
        {
            this.responseText.Should().Be(this.block.ToString());
            this.responseText.Should().Be(this.nodes[SendingNode].FullNode.ConsensusLoop().Tip.HashBlock.ToString());
        }

        private void the_blockcount_should_match_consensus_tip_height()
        {
            this.responseText.Should().Be(this.nodes[SendingNode].FullNode.ConsensusLoop().Tip.Height.ToString());
        }

        private void send_api_get_request(string apiendpoint)
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetAsync($"{this.apiUri}{apiendpoint}").GetAwaiter().GetResult();
            this.response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            this.responseText = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
    }
}