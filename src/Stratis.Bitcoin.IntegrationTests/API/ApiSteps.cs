using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.Api;
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
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private const string PosNode = "pos_node";
        private const string FirstPowNode = "first_pow_node";
        private const string SecondPowNode = "second_pow_node";
        private const string PrimaryWalletName = "wallet_name";
        private const string SecondaryWalletName = "secondary_wallet_name";
        private const string WalletAccountName = "account 0";
        private const string WalletPassword = "wallet_password";
        private const string StratisRegTest = "StratisRegTest";

        private IDictionary<string, CoreNode> nodes;

        private HttpResponseMessage response;
        private string responseText;
        private HttpResponseMessage postResponse;

        private int maturity;
        private HdAddress receiverAddress;
        private readonly Money transferAmount = Money.COIN * 1;
        private NodeGroupBuilder nodeGroupBuilder;
        private SharedSteps sharedSteps;
        private Transaction transaction;
        private uint256 block;
        private Uri apiUri;
        private HttpClient httpClient;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
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

        private void two_connected_pow_nodes_with_api_enabled()
        {
            a_pow_node_with_api_enabled();
            a_second_pow_node_with_api_enabled();
            calling_addnode_via_api_connects_two_nodes();

            this.receiverAddress = this.nodes[SecondPowNode].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(SecondaryWalletName, WalletAccountName));
        }

        private void a_pow_node_with_api_enabled()
        {
            this.nodes = this.nodeGroupBuilder
                .CreateStratisPowApiNode(FirstPowNode)
                .Start()
                .NotInIBD()
                .WithWallet(PrimaryWalletName, WalletPassword)
                .Build();

            this.maturity = 1;

            this.nodes[FirstPowNode].FullNode
                .Network.Consensus.CoinbaseMaturity = 1;

            this.nodes[FirstPowNode].SetDummyMinerSecret(new BitcoinSecret(new Key(), this.nodes[FirstPowNode].FullNode.Network));

            this.apiUri = this.nodes[FirstPowNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void a_second_pow_node_with_api_enabled()
        {
            this.nodes = this.nodeGroupBuilder
                .CreateStratisPowApiNode(SecondPowNode)
                .Start()
                .NotInIBD()
                .WithWallet(SecondaryWalletName, WalletPassword)
                .Build();
        }

        protected void a_block_is_mined_creating_spendable_coins()
        {
            this.sharedSteps.MineBlocks(1, this.nodes[FirstPowNode], WalletAccountName, PrimaryWalletName, WalletPassword);
        }

        private void more_blocks_mined_past_maturity_of_original_block()
        {
            this.sharedSteps.MineBlocks(this.maturity, this.nodes[FirstPowNode], WalletAccountName, PrimaryWalletName, WalletPassword);
        }

        private void a_real_transaction()
        {
            this.SendTransaction(this.BuildTransaction());
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.block = this.nodes[FirstPowNode].GenerateStratisWithMiner(1).Single();
            this.nodes[FirstPowNode].GenerateStratisWithMiner(1);
        }

        private void calling_startstaking_via_api()
        {
            var stakingRequest = new StartStakingRequest() { Name = PrimaryWalletName, Password = WalletPassword };

            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet(WalletPassword, PrimaryWalletName);

            var httpRequestContent = new StringContent(stakingRequest.ToString(), Encoding.UTF8, JsonContentType);
            this.response = this.httpClient.PostAsync($"{this.apiUri}api/miner/startstaking", httpRequestContent).GetAwaiter().GetResult();

            this.response.StatusCode.Should().Be(HttpStatusCode.OK);
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

        private void calling_recover_via_extpubkey_for_account_0()
        {
            this.RecoverViaExtPubKey(PrimaryWalletName, "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1", 0);
        }

        private void attempting_to_add_an_account()
        {
            var request = new GetUnusedAccountModel()
            {
                WalletName = PrimaryWalletName,
                Password = WalletPassword
            };

            this.postResponse = this.httpClient.PostAsJsonAsync($"{this.apiUri}api/Wallet/account", request)
                .GetAwaiter().GetResult();
        }

        private void an_extpubkey_only_wallet_with_account_0()
        {
            this.RecoverViaExtPubKey(PrimaryWalletName, "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1", 0);
        }

        private void calling_recover_via_extpubkey_for_account_1()
        {
            //NOTE: use legacy stratis xpub key format for this one to ensure that works too.
            this.RecoverViaExtPubKey(SecondaryWalletName, "xq5hcJV8uJDLaNytrg6FphHY1vdqxP1rCPhAmp4xZwpxzYyYEscYEujAmNR5NrPfy9vzQ6BajEqtFezcyRe4zcGHH3dR6BKaKov43JHd8UYhBVy", 1);
        }

        private void RecoverViaExtPubKey(string walletName, string extPubKey, int accountIndex)
        {
            var request = new WalletExtPubRecoveryRequest
            {
                ExtPubKey = extPubKey,
                AccountIndex = accountIndex,
                Name = walletName
            };

            this.send_api_post_request("api/Wallet/recover-via-extpubkey", request);
            this.postResponse.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        private void send_api_post_request<T>(string url, T request)
        {
            this.postResponse = this.httpClient.PostAsJsonAsync($"{this.apiUri}{url}", request)
                .GetAwaiter().GetResult();
        }

        private void a_wallet_is_created_without_private_key_for_account_0()
        {
            this.CheckAccountExists(PrimaryWalletName, 0);
        }

        private void a_wallet_is_created_without_private_key_for_account_1()
        {
            this.CheckAccountExists(SecondaryWalletName, 1);
        }

        private void CheckAccountExists(string walletName, int accountIndex)
        {
            this.send_api_get_request($"api/Wallet/balance?walletname={walletName}&AccountName=account {accountIndex}");

            this.responseText.Should()
                .Be("{\"balances\":[{\"accountName\":\"account " + accountIndex + "\",\"accountHdPath\":\"m/44'/105'/" + accountIndex + "'\",\"coinType\":105,\"amountConfirmed\":0,\"amountUnconfirmed\":0}]}");
        }

        private void calling_general_info_via_api()
        {
            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet(WalletPassword, PrimaryWalletName);
            this.send_api_get_request($"api/wallet/general-info?name={PrimaryWalletName}");
        }

        private void calling_addnode_via_api_connects_two_nodes()
        {
            this.send_api_get_request($"api/ConnectionManager/addnode?endpoint={this.nodes[SecondPowNode].Endpoint.ToString()}&command=onetry");
            this.responseText.Should().Be("true");
            this.WaitForNodeToSync(this.nodes[FirstPowNode], this.nodes[SecondPowNode]);
        }

        private void calling_block_with_valid_hash_via_api_returns_block()
        {
            this.send_api_get_request($"api/BlockStore/block?Hash={this.block}&OutputJson=true");
        }

        private void calling_getblockcount_via_api_returns_an_int()
        {
            this.send_api_get_request("api/BlockStore/getblockcount");
        }

        private void calling_getbestblockhash_via_api()
        {
            this.send_api_get_request("api/Consensus/getbestblockhash");
        }

        private void calling_getpeerinfo_via_api()
        {
            this.send_api_get_request("api/ConnectionManager/getpeerinfo");
        }

        private void calling_getblockhash_via_api()
        {
            this.send_api_get_request("api/Consensus/getblockhash?height=0");
        }

        private void calling_getblockheader_via_api()
        {
            this.send_api_get_request($"api/Node/getblockheader?hash={NetworkContainer.RegTest.Consensus.HashGenesisBlock.ToString()}");
        }

        private void calling_status_via_api()
        {
            this.send_api_get_request("api/Node/status");
        }

        private void calling_validateaddress_via_api()
        {
            string address = this.nodes[FirstPowNode].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(PrimaryWalletName, WalletAccountName))
                .ScriptPubKey.GetDestinationAddress(this.nodes[FirstPowNode].FullNode.Network).ToString();
            this.send_api_get_request($"api/Node/validateaddress?address={address}");
        }

        private void calling_getrawmempool_via_api()
        {
            this.send_api_get_request("api/Mempool/getrawmempool");
        }

        private void calling_gettxout_notmempool_via_api()
        {
            this.send_api_get_request($"api/Node/gettxout?trxid={this.transaction.GetHash().ToString()}&vout=1&includeMemPool=false");
        }
        private void calling_getrawtransaction_nonverbose_via_api()
        {
            this.send_api_get_request($"api/Node/getrawtransaction?trxid={this.transaction.GetHash().ToString()}&verbose=false");
        }

        private void calling_getrawtransaction_verbose_via_api()
        {
            this.send_api_get_request($"api/Node/getrawtransaction?trxid={this.transaction.GetHash().ToString()}&verbose=true");
        }

        private void a_valid_address_is_validated()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["isvalid"].Value<bool>().Should().BeTrue();
        }

        private void the_consensus_tip_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + this.nodes[FirstPowNode].FullNode.ConsensusLoop().Tip.HashBlock.ToString() + "\"");
        }

        private void the_blockcount_should_match_consensus_tip_height()
        {
            this.responseText.Should().Be(this.nodes[FirstPowNode].FullNode.ConsensusLoop().Tip.Height.ToString());
        }

        private void the_real_block_should_be_retrieved()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["hash"].Value<string>()
                .Should().Be(this.block.ToString());
        }

        private void the_block_should_contain_the_transaction()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["transactions"][1].Value<string>()
                .Should().Be(this.transaction.GetHash().ToString());
        }

        private void it_is_rejected_as_forbidden()
        {
            this.postResponse.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        private void the_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + NetworkContainer.RegTest.Consensus.HashGenesisBlock.ToString() + "\"");
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

        private void status_information_is_returned()
        {
            var statusNode = this.nodes[FirstPowNode].FullNode;
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["agent"].Value<string>().Should().Contain(statusNode.Settings.Agent);
            jObjectResponse["version"].Value<string>().Should().Be(statusNode.Version.ToString());
            jObjectResponse["network"].Value<string>().Should().Be(statusNode.Network.Name);
            jObjectResponse["consensusHeight"].Value<int>().Should().Be(0);
            jObjectResponse["blockStoreHeight"].Value<int>().Should().Be(0);
            jObjectResponse["protocolVersion"].Value<uint>().Should().Be((uint)(statusNode.Settings.ProtocolVersion));
            jObjectResponse["relayFee"].Value<decimal>().Should().Be(statusNode.Settings.MinRelayTxFeeRate.FeePerK.ToUnit(MoneyUnit.BTC));
            jObjectResponse["dataDirectoryPath"].Value<string>().Should().Be(statusNode.Settings.DataDir);
            JArray jArrayFeatures = jObjectResponse["enabledFeatures"].Value<JArray>();
            jArrayFeatures.Contains("Stratis.Bitcoin.Base.BaseFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.Api.ApiFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.BlockStore.BlockStoreFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.Consensus.ConsensusFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.MemoryPool.MempoolFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.Miner.MiningFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.RPC.RPCFeature");
            jArrayFeatures.Contains("Stratis.Bitcoin.Features.Wallet.WalletFeature");
        }

        private void general_information_about_the_wallet_and_node_is_returned()
        {
            var generalInfoResponse = JsonDataSerializer.Instance.Deserialize<WalletGeneralInfoModel>(this.responseText);

            generalInfoResponse.WalletFilePath.Should().ContainAll(StratisRegTest, $"{PrimaryWalletName}.wallet.json");
            generalInfoResponse.Network.Name.Should().Be(StratisRegTest);
            generalInfoResponse.ChainTip.Should().Be(0);
            generalInfoResponse.IsChainSynced.Should().BeFalse();
            generalInfoResponse.ConnectedNodes.Should().Be(0);
            generalInfoResponse.IsDecrypted.Should().BeTrue();
        }

        private void the_blockheader_is_returned()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["previousblockhash"].Value<string>().Should()
                .Be("0000000000000000000000000000000000000000000000000000000000000000");
        }

        private void the_transaction_is_found_in_mempool()
        {
            JArray jArrayResponse = JArray.Parse(this.responseText);
            jArrayResponse[0].Value<string>().Should()
                .Be(this.transaction.GetHash().ToString());
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.nodes[PosNode].FullNode.NodeService<MiningRPCController>();
            GetStakingInfoModel stakingInfo = miningRpcController.GetStakingInfo();

            stakingInfo.Should().NotBeNull();
            stakingInfo.Enabled.Should().BeTrue();
            stakingInfo.Staking.Should().BeFalse();
        }

        private void the_transaction_hash_is_returned()
        {
            this.responseText.Should().Be("\"" + this.transaction.ToHex() + "\"");
        }

        private void a_verbose_raw_transaction_is_returned()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["hex"].Value<string>().Should().Be(this.transaction.ToHex());
            jObjectResponse["txid"].Value<string>().Should().Be(this.transaction.GetHash().ToString());
        }

        private void a_single_connected_peer_is_returned()
        {
            JArray jArrayResponse = JArray.Parse(this.responseText);
            jArrayResponse.Count.Should().Be(1);
            jArrayResponse[0]["id"].Value<int>()
                .Should().Be(0);
            jArrayResponse[0]["addr"].Value<string>()
                .Should().Contain("[::ffff:127.0.0.1]");
        }

        private void the_txout_is_returned()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["value"].Value<long>().Should()
                .Be(this.transferAmount.Satoshi);
        }

        private void send_api_get_request(string apiendpoint)
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetAsync($"{this.apiUri}{apiendpoint}").GetAwaiter().GetResult();
            this.response.StatusCode.Should().Be(HttpStatusCode.OK);
            this.responseText = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        private void WaitForNodeToSync(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(n =>
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(n)));
            nodes.Skip(1).ToList().ForEach(
                n => TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(nodes.First(), n)));
        }

        private void SendTransaction(IActionResult transactionResult)
        {
            var walletTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;
            this.transaction = this.nodes[FirstPowNode].FullNode.Network.CreateTransaction(walletTransactionModel.Hex);
            this.nodes[FirstPowNode].FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        private IActionResult BuildTransaction()
        {
            IActionResult transactionResult = this.nodes[FirstPowNode].FullNode.NodeService<WalletController>()
                .BuildTransaction(new BuildTransactionRequest
                {
                    AccountName = WalletAccountName,
                    AllowUnconfirmed = true,
                    ShuffleOutputs = false,
                    Amount = this.transferAmount.ToString(),
                    DestinationAddress = this.receiverAddress.Address,
                    FeeType = FeeType.Medium.ToString("D"),
                    Password = WalletPassword,
                    WalletName = PrimaryWalletName,
                    FeeAmount = Money.Satoshis(82275).ToString() // Minimum fee
                });
            return transactionResult;
        }
    }
}
