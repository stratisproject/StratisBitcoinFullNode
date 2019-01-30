using System;
using System.Collections.Generic;
using System.IO;
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
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.TestNetworks;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private const string WalletName = "mywallet";
        private const string WalletAccountName = "account 0";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "wallet_passphrase";
        private const string StratisRegTest = "StratisRegTest";

        // BlockStore
        private const string BlockUri = "api/blockstore/block";
        private const string GetBlockCountUri = "api/blockstore/getblockcount";

        // ConnectionManager
        private const string AddnodeUri = "api/connectionmanager/addnode";
        private const string GetPeerInfoUri = "api/connectionmanager/getpeerinfo";

        // Consensus
        private const string GetBestBlockHashUri = "api/consensus/getbestblockhash";
        private const string GetBlockHashUri = "api/consensus/getblockhash";

        // Mempool
        private const string GetRawMempoolUri = "api/mempool/getrawmempool";

        // Mining
        private const string GenerateUri = "api/mining/generate";

        // Node
        private const string GetBlockHeaderUri = "api/node/getblockheader";
        private const string GetRawTransactionUri = "api/node/getrawtransaction";
        private const string GetTxOutUri = "api/node/gettxout";
        private const string StatusUri = "api/node/status";
        private const string ValidateAddressUri = "api/node/validateaddress";

        // RPC
        private const string RPCCallByNameUri = "api/rpc/callbyname";
        private const string RPCListmethodsUri = "api/rpc/listmethods";

        // Staking
        private const string StartStakingUri = "api/staking/startstaking";
        private const string GetStakingInfoUri = "api/staking/getstakinginfo";

        // Wallet
        private const string AccountUri = "api/wallet/account";
        private const string GeneralInfoUri = "api/wallet/general-info";
        private const string BalanceUri = "api/wallet/balance";
        private const string RecoverViaExtPubKeyUri = "api/wallet/recover-via-extpubkey";

        private CoreNode stratisPosApiNode;
        private CoreNode firstStratisPowApiNode;
        private CoreNode secondStratisPowApiNode;

        private HttpResponseMessage response;
        private string responseText;

        private int maturity = 1;
        private HdAddress receiverAddress;
        private readonly Money transferAmount = Money.COIN * 1;
        private NodeBuilder powNodeBuilder;
        private NodeBuilder posNodeBuilder;

        private Transaction transaction;
        private uint256 block;
        private Uri apiUri;
        private HttpClient httpClient;
        private HttpClientHandler httpHandler;
        private Network powNetwork;
        private Network posNetwork;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.httpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true };
            this.httpClient = new HttpClient(this.httpHandler);
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));

            this.powNodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
            this.posNodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));

            this.powNetwork = new BitcoinRegTestOverrideCoinbaseMaturity(1);
            this.posNetwork = new StratisRegTest();
        }

        protected override void AfterTest()
        {
            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            if (this.httpHandler != null)
            {
                this.httpHandler.Dispose();
                this.httpHandler = null;
            }

            this.powNodeBuilder.Dispose();
            this.posNodeBuilder.Dispose();
        }

        private void a_proof_of_stake_node_with_api_enabled()
        {
            this.stratisPosApiNode = this.posNodeBuilder.CreateStratisPosNode(this.posNetwork).Start();

            this.stratisPosApiNode.FullNode.NodeService<IPosMinting>(true).Should().NotBeNull();
            this.apiUri = this.stratisPosApiNode.FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void the_proof_of_stake_node_has_passed_LastPOWBlock()
        {
            typeof(ChainedHeader).GetProperty("Height").SetValue(this.stratisPosApiNode.FullNode.ConsensusManager().Tip,
                this.stratisPosApiNode.FullNode.Network.Consensus.LastPOWBlock + 1);
        }

        private void two_connected_proof_of_work_nodes_with_api_enabled()
        {
            a_proof_of_work_node_with_api_enabled();
            a_second_proof_of_work_node_with_api_enabled();
            calling_addnode_connects_two_nodes();

            this.receiverAddress = this.secondStratisPowApiNode.FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(WalletName, WalletAccountName));
        }

        private void a_proof_of_work_node_with_api_enabled()
        {
            this.firstStratisPowApiNode = this.powNodeBuilder.CreateStratisPowNode(this.powNetwork).WithWallet().Start();
            this.firstStratisPowApiNode.Mnemonic = this.firstStratisPowApiNode.Mnemonic;

            this.firstStratisPowApiNode.FullNode.Network.Consensus.CoinbaseMaturity = this.maturity;
            this.apiUri = this.firstStratisPowApiNode.FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void a_second_proof_of_work_node_with_api_enabled()
        {
            this.secondStratisPowApiNode = this.powNodeBuilder.CreateStratisPowNode(this.powNetwork).WithWallet().Start();
            this.secondStratisPowApiNode.Mnemonic = this.secondStratisPowApiNode.Mnemonic;
        }

        protected void a_block_is_mined_creating_spendable_coins()
        {
            TestHelper.MineBlocks(this.firstStratisPowApiNode, 1);
        }

        private void more_blocks_mined_past_maturity_of_original_block()
        {
            TestHelper.MineBlocks(this.firstStratisPowApiNode, this.maturity);
        }

        private void a_real_transaction()
        {
            this.SendTransaction(this.BuildTransaction());
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.block = TestHelper.MineBlocks(this.firstStratisPowApiNode, 2).BlockHashes[0];
        }

        private void calling_startstaking()
        {
            var stakingRequest = new StartStakingRequest() { Name = WalletName, Password = WalletPassword };

            // With these tests we still need to create the wallets outside of the builder
            this.stratisPosApiNode.Mnemonic = this.stratisPosApiNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName, WalletPassphrase);

            var httpRequestContent = new StringContent(stakingRequest.ToString(), Encoding.UTF8, JsonContentType);
            this.response = this.httpClient.PostAsync($"{this.apiUri}{StartStakingUri}", httpRequestContent).GetAwaiter().GetResult();

            this.response.StatusCode.Should().Be(HttpStatusCode.OK);
            this.responseText = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            this.responseText.Should().BeEmpty();
        }

        private void calling_rpc_getblockhash_via_callbyname()
        {
            this.send_api_post_request(RPCCallByNameUri, new { methodName = "getblockhash", height = 0 });
        }

        private void calling_rpc_listmethods()
        {
            this.send_api_get_request($"{RPCListmethodsUri}");
        }

        private void calling_recover_via_extpubkey_for_account_0()
        {
            this.RecoverViaExtPubKey(WalletName, "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1", 0);
        }

        private void attempting_to_add_an_account()
        {
            var request = new GetUnusedAccountModel()
            {
                WalletName = WalletName,
                Password = WalletPassword
            };

            this.response = this.httpClient.PostAsJsonAsync($"{this.apiUri}{AccountUri}", request)
                .GetAwaiter().GetResult();
        }

        private void an_extpubkey_only_wallet_with_account_0()
        {
            this.RecoverViaExtPubKey(WalletName, "xpub6DGguHV1FQFPvZ5Xu7VfeENyiySv4R2bdd6VtvwxWGVTVNnHUmphMNgTRkLe8j2JdAv332ogZcyhqSuz1yUPnN4trJ49cFQXmEhwNQHUqk1", 0);
        }

        private void calling_recover_via_extpubkey_for_account_1()
        {
            //NOTE: use legacy stratis xpub key format for this one to ensure that works too.
            this.RecoverViaExtPubKey("Secondary_Wallet", "xq5hcJV8uJDLaNytrg6FphHY1vdqxP1rCPhAmp4xZwpxzYyYEscYEujAmNR5NrPfy9vzQ6BajEqtFezcyRe4zcGHH3dR6BKaKov43JHd8UYhBVy", 1);
        }

        private void RecoverViaExtPubKey(string walletName, string extPubKey, int accountIndex)
        {
            var request = new WalletExtPubRecoveryRequest
            {
                ExtPubKey = extPubKey,
                AccountIndex = accountIndex,
                Name = walletName,
                CreationDate = DateTime.UtcNow
            };

            this.send_api_post_request(RecoverViaExtPubKeyUri, request);
            this.response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        private void send_api_post_request<T>(string url, T request)
        {
            this.response = this.httpClient.PostAsJsonAsync($"{this.apiUri}{url}", request)
                .GetAwaiter().GetResult();
        }

        private void a_wallet_is_created_without_private_key_for_account_0()
        {
            this.CheckAccountExists(WalletName, 0);
        }

        private void a_wallet_is_created_without_private_key_for_account_1()
        {
            this.CheckAccountExists("Secondary_Wallet", 1);
        }

        private void CheckAccountExists(string walletName, int accountIndex)
        {
            this.send_api_get_request($"{BalanceUri}?walletname={walletName}&AccountName=account {accountIndex}");

            this.responseText.Should().Be("{\"balances\":[{\"accountName\":\"account " + accountIndex + "\",\"accountHdPath\":\"m/44'/105'/" + accountIndex + "'\",\"coinType\":105,\"amountConfirmed\":0,\"amountUnconfirmed\":0}]}");
        }

        private void calling_general_info()
        {
            // With these tests we still need to create the wallets outside of the builder
            this.stratisPosApiNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName, WalletPassphrase);
            this.send_api_get_request($"{GeneralInfoUri}?name={WalletName}");
        }

        private void calling_addnode_connects_two_nodes()
        {
            this.send_api_get_request($"{AddnodeUri}?endpoint={this.secondStratisPowApiNode.Endpoint.ToString()}&command=onetry");
            this.responseText.Should().Be("true");

            TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.firstStratisPowApiNode, this.secondStratisPowApiNode));
        }

        private void calling_block()
        {
            this.send_api_get_request($"{BlockUri}?Hash={this.block}&OutputJson=true");
        }

        private void calling_getblockcount()
        {
            this.send_api_get_request(GetBlockCountUri);
        }

        private void calling_getbestblockhash()
        {
            this.send_api_get_request(GetBestBlockHashUri);
        }

        private void calling_getpeerinfo()
        {
            this.send_api_get_request(GetPeerInfoUri);
        }

        private void calling_getblockhash()
        {
            this.send_api_get_request($"{GetBlockHashUri}?height=0");
        }

        private void calling_getblockheader()
        {
            this.send_api_get_request($"{GetBlockHeaderUri}?hash={KnownNetworks.RegTest.Consensus.HashGenesisBlock.ToString()}");
        }

        private void calling_status()
        {
            this.send_api_get_request(StatusUri);
        }

        private void calling_validateaddress()
        {
            string address = this.firstStratisPowApiNode.FullNode.WalletManager()
                .GetUnusedAddress()
                .ScriptPubKey.GetDestinationAddress(this.firstStratisPowApiNode.FullNode.Network).ToString();
            this.send_api_get_request($"{ValidateAddressUri}?address={address}");
        }

        private void calling_getrawmempool()
        {
            this.send_api_get_request(GetRawMempoolUri);
        }

        private void calling_gettxout_notmempool()
        {
            this.send_api_get_request($"{GetTxOutUri}?trxid={this.transaction.GetHash().ToString()}&vout=1&includeMemPool=false");
        }

        private void calling_getrawtransaction_nonverbose()
        {
            this.send_api_get_request($"{GetRawTransactionUri}?trxid={this.transaction.GetHash().ToString()}&verbose=false");
        }

        private void calling_getrawtransaction_verbose()
        {
            this.send_api_get_request($"{GetRawTransactionUri}?trxid={this.transaction.GetHash().ToString()}&verbose=true");
        }

        private void calling_getstakinginfo()
        {
            this.send_api_get_request(GetStakingInfoUri);
        }

        private void calling_generate()
        {
            var request = new MiningRequest() { BlockCount = 1 };
            this.send_api_post_request(GenerateUri, request);
        }

        private void a_valid_address_is_validated()
        {
            JObject jObjectResponse = JObject.Parse(this.responseText);
            jObjectResponse["isvalid"].Value<bool>().Should().BeTrue();
        }

        private void the_consensus_tip_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + this.firstStratisPowApiNode.FullNode.ConsensusManager().Tip.HashBlock.ToString() + "\"");
        }

        private void the_blockcount_should_match_consensus_tip_height()
        {
            this.responseText.Should().Be(this.firstStratisPowApiNode.FullNode.ConsensusManager().Tip.Height.ToString());
        }

        private void the_real_block_should_be_retrieved()
        {
            var blockResponse = JsonDataSerializer.Instance.Deserialize<BlockModel>(this.responseText);
            blockResponse.Hash.Should().Be(this.block.ToString());
        }

        private void the_block_should_contain_the_transaction()
        {
            var blockResponse = JsonDataSerializer.Instance.Deserialize<BlockModel>(this.responseText);
            blockResponse.Transactions[1].Should().Be(this.transaction.GetHash().ToString());
        }

        private void it_is_rejected_as_forbidden()
        {
            this.response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        private void the_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + KnownNetworks.RegTest.Consensus.HashGenesisBlock.ToString() + "\"");
        }

        private void the_blockhash_is_returned_from_post()
        {
            var responseContent = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            responseContent.Should().Be("\"" + KnownNetworks.RegTest.Consensus.HashGenesisBlock.ToString() + "\"");
        }

        private void a_full_list_of_available_commands_is_returned()
        {
            var commands = JsonDataSerializer.Instance.Deserialize<List<RpcCommandModel>>(this.responseText);

            commands.Count.Should().Be(27);
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
            commands.Should().Contain(x => x.Command == "sendtoaddress <address> <amount> <commenttx> <commentdest>");
            commands.Should().Contain(x => x.Command == "getnewaddress <account> <addresstype>");
            commands.Should().Contain(x => x.Command == "sendrawtransaction <hex>");
            commands.Should().Contain(x => x.Command == "decoderawtransaction <hex>");
            commands.Should().Contain(x => x.Command == "getblock <blockhash> [<isjsonformat>]");
            commands.Should().Contain(x => x.Command == "walletlock");
            commands.Should().Contain(x => x.Command == "walletpassphrase <passphrase> <timeout>");
            commands.Should().Contain(x => x.Command == "listunspent [<minconfirmations>] [<maxconfirmations>] [<addressesjson>]");
            commands.Should().Contain(x => x.Command == "sendmany <fromaccount> <addressesjson> [<minconf>] [<comment>] [<subtractfeefromjson>] [<isreplaceable>] [<conftarget>] [<estimatemode>]");
        }

        private void status_information_is_returned()
        {
            var statusNode = this.firstStratisPowApiNode.FullNode;
            var statusResponse = JsonDataSerializer.Instance.Deserialize<StatusModel>(this.responseText);
            statusResponse.Agent.Should().Contain(statusNode.Settings.Agent);
            statusResponse.Version.Should().Be(statusNode.Version.ToString());
            statusResponse.Network.Should().Be(statusNode.Network.Name);
            statusResponse.ConsensusHeight.Should().Be(0);
            statusResponse.BlockStoreHeight.Should().Be(0);
            statusResponse.ProtocolVersion.Should().Be((uint)(statusNode.Settings.ProtocolVersion));
            statusResponse.RelayFee.Should().Be(statusNode.Settings.MinRelayTxFeeRate.FeePerK.ToUnit(MoneyUnit.BTC));
            statusResponse.DataDirectoryPath.Should().Be(statusNode.Settings.DataDir);
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Base.BaseFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.Api.ApiFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.BlockStore.BlockStoreFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.Consensus.PowConsensusFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.MemoryPool.MempoolFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.Miner.MiningFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.RPC.RPCFeature");
            statusResponse.EnabledFeatures.Should().Contain("Stratis.Bitcoin.Features.Wallet.WalletFeature");
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

        private void the_blockheader_is_returned()
        {
            var blockheaderResponse = JsonDataSerializer.Instance.Deserialize<BlockHeaderModel>(this.responseText);
            blockheaderResponse.PreviousBlockHash.Should()
                .Be("0000000000000000000000000000000000000000000000000000000000000000");
        }

        private void the_transaction_is_found_in_mempool()
        {
            List<string> transactionList = JArray.Parse(this.responseText).ToObject<List<string>>();
            transactionList[0].Should().Be(this.transaction.GetHash().ToString());
        }

        private void staking_is_enabled_but_nothing_is_staked()
        {
            var miningRpcController = this.stratisPosApiNode.FullNode.NodeService<StakingRpcController>();
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
            var verboseRawTransactionResponse = JsonDataSerializer.Instance.Deserialize<TransactionVerboseModel>(this.responseText);
            verboseRawTransactionResponse.Hex.Should().Be(this.transaction.ToHex());
            verboseRawTransactionResponse.TxId.Should().Be(this.transaction.GetHash().ToString());
        }

        private void a_single_connected_peer_is_returned()
        {
            List<PeerNodeModel> getPeerInfoResponseList = JArray.Parse(this.responseText).ToObject<List<PeerNodeModel>>();
            getPeerInfoResponseList.Count.Should().Be(1);
            getPeerInfoResponseList[0].Id.Should().Be(0);
            getPeerInfoResponseList[0].Address.Should().Contain("[::ffff:127.0.0.1]");
        }

        private void the_txout_is_returned()
        {
            var txOutResponse = JsonDataSerializer.Instance.Deserialize<GetTxOutModel>(this.responseText);
            txOutResponse.Value.Should().Be(this.transferAmount);
        }

        private void staking_information_is_returned()
        {
            var stakingInfoModel = JsonDataSerializer.Instance.Deserialize<GetStakingInfoModel>(this.responseText);
            stakingInfoModel.Enabled.Should().Be(false);
            stakingInfoModel.Staking.Should().Be(false);
        }

        private void a_method_not_allowed_error_is_returned()
        {
            this.response.StatusCode.Should().Be(StatusCodes.Status405MethodNotAllowed);
        }

        private void send_api_get_request(string apiendpoint)
        {
            this.response = this.httpClient.GetAsync($"{this.apiUri}{apiendpoint}").GetAwaiter().GetResult();
            if (this.response.IsSuccessStatusCode)
            {
                this.responseText = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }

        private void SendTransaction(IActionResult transactionResult)
        {
            var walletTransactionModel = (WalletBuildTransactionModel)(transactionResult as JsonResult)?.Value;
            this.transaction = this.firstStratisPowApiNode.FullNode.Network.CreateTransaction(walletTransactionModel.Hex);
            this.firstStratisPowApiNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(walletTransactionModel.Hex));
        }

        private IActionResult BuildTransaction()
        {
            IActionResult transactionResult = this.firstStratisPowApiNode.FullNode.NodeService<WalletController>()
                .BuildTransaction(new BuildTransactionRequest
                {
                    AccountName = WalletAccountName,
                    AllowUnconfirmed = true,
                    ShuffleOutputs = false,
                    Recipients = new List<RecipientModel> { new RecipientModel { DestinationAddress = this.receiverAddress.Address, Amount = this.transferAmount.ToString() } },
                    FeeType = FeeType.Medium.ToString("D"),
                    Password = WalletPassword,
                    WalletName = WalletName,
                    FeeAmount = Money.Satoshis(82275).ToString() // Minimum fee
                });
            return transactionResult;
        }
    }
}
