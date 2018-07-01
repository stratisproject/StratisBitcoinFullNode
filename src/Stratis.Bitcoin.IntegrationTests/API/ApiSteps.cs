using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NBitcoin;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;
using static Stratis.Bitcoin.Features.Miner.PosMinting;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private const string PosNode = "pos_node";
        private const string ReceivingNode = "receiving_node";
        private const string SendingWalletName = "sending_wallet_name";
        private const string ReceivingWalletName = "receiving_wallet_name";
        private const string WalletAccountName = "account 0";
        private const string WalletPassword = "wallet_password";
        private const string StratisRegTest = "StratisRegTest";
        private const decimal OneMillion = 1_000_000;

        private ProofOfStakeSteps proofOfStakeSteps;

        private HttpClient httpClient;
        private Uri apiUri;
        private string response;
        private NodeGroupBuilder nodeGroupBuilder;
        private IDictionary<string, CoreNode> nodes;
        private List<uint256> blockIds;
        private string password = "P@ssw0rd";
        private WalletAccountReference miningWalletAccountReference;
        private HdAddress minerAddress;
        private Features.Wallet.Wallet miningWallet;
        private Key key;
        private int maturity;
        private readonly Money transferAmount = Money.COIN * 2;
        private Transaction transaction;
        private HdAddress receiverAddress;
        private uint256 blockWithTransactionId;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.proofOfStakeSteps = new ProofOfStakeSteps(this.CurrentTest.DisplayName);
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
            this.proofOfStakeSteps.NodeGroupBuilder?.Dispose();

        }

        private void a_proof_of_stake_node_with_api_enabled()
        {
            this.nodes = this.nodeGroupBuilder.CreateStratisPosApiNode(PosNode)
                .Start()
                .WithWallet(SendingWalletName, WalletPassword)
                .Build();

            this.nodes[PosNode].FullNode.NodeService<IPosMinting>(true)
                .Should().NotBeNull();

            this.apiUri = this.nodes[PosNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void preminenode_and_receiving_node_with_api_enabled()
        {
            this.proofOfStakeSteps.GenerateCoins();

            this.proofOfStakeSteps.PremineNodeWithCoins.FullNode.WalletManager()
                .GetSpendableTransactionsInWallet(this.proofOfStakeSteps.PremineWallet)
                .Sum(utxo => utxo.Transaction.Amount)
                .Should().BeGreaterThan(Money.Coins(OneMillion));

            this.nodes[ReceivingNode] = this.proofOfStakeSteps.NodeGroupBuilder
                                    .CreateStratisPosNode(ReceivingNode)
                                    .Start()
                                    .NotInIBD()
                                    .WithWallet(ReceivingWalletName, WalletPassword)
                                    .Build()[ReceivingNode];

            this.nodes = this.nodeGroupBuilder
                .CreateStratisPosNode(ReceivingNode).Start()
                .WithWallet(ReceivingWalletName, WalletPassword)
                .WithConnections()
                .Connect(PosNode, ReceivingNode)
                .AndNoMoreConnections()
                .Build();

            this.receiverAddress = this.nodes[ReceivingNode].FullNode.WalletManager()
                .GetUnusedAddress(new WalletAccountReference(ReceivingWalletName, WalletAccountName));

            this.maturity = (int)this.nodes[PosNode].FullNode
                .Network.Consensus.CoinbaseMaturity;

            this.apiUri = this.nodes[PosNode].FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void getting_general_info()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/wallet/general-info?name={SendingWalletName}").GetAwaiter().GetResult();
        }

        private void general_information_about_the_wallet_and_node_is_returned()
        {
            var generalInfoResponse = JsonDataSerializer.Instance.Deserialize<WalletGeneralInfoModel>(this.response);

            generalInfoResponse.WalletFilePath.Should().ContainAll(StratisRegTest, $"{SendingWalletName}.wallet.json");
            generalInfoResponse.Network.Name.Should().Be(StratisRegTest);
            generalInfoResponse.ChainTip.Should().Be(0);
            generalInfoResponse.IsChainSynced.Should().BeFalse();
            generalInfoResponse.ConnectedNodes.Should().Be(0);
            generalInfoResponse.IsDecrypted.Should().BeTrue();
        }

        private void staking_is_started()
        {
            var stakingRequest = new StartStakingRequest() { Name = SendingWalletName, Password = WalletPassword };

            var httpRequestContent = new StringContent(stakingRequest.ToString(), Encoding.UTF8, JsonContentType);
            HttpResponseMessage stakingResponse = this.httpClient.PostAsync($"{this.apiUri}api/miner/startstaking", httpRequestContent).GetAwaiter().GetResult();

            stakingResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            string responseText = stakingResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
            GetStakingInfoModel stakingInfo = miningRpcController.GetStakingInfo();

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

        public void some_real_blocks_with_a_uint256_identifier()
        {
            this.maturity = (int)this.nodes[PosNode].FullNode.Network.Consensus.CoinbaseMaturity;
            this.blockIds = this.nodes[PosNode].GenerateStratisWithMiner(this.maturity + 1);
        }

        public void some_blocks_creating_reward()
        {
            this.some_real_blocks_with_a_uint256_identifier();
        }

        public void genesis_is_mined()
        {
            this.sharedSteps.MinePremineBlocks(this.nodes[PosNode], SendingWalletName, "account 0", WalletPassword);
        }

        public void coins_are_mined_to_maturity()
        {
            this.nodes[PosNode].GenerateStratisWithMiner(100);
            this.sharedSteps.WaitForNodeToSync(this.nodes[PosNode]);
        }

        public void coins_are_mined_past_maturity()
        {
            this.nodes[PosNode].GenerateStratisWithMiner(Convert.ToInt32(this.nodes[PosNode].FullNode.Network.Consensus.CoinbaseMaturity));
        }

    public void the_pos_node_starts_staking()
        {
            var minter = this.nodes[PosNode].FullNode.NodeService<IPosMinting>();
            minter.Stake(new WalletSecret() { WalletName = SendingWalletName, WalletPassword = WalletPassword });
        }

        public void the_node_is_synced()
        {
            this.sharedSteps.WaitForNodeToSync(this.nodes[PosNode]);
        }

        public void the_nodes_are_synced()
        {
            this.sharedSteps.WaitForNodeToSync(this.nodes[PosNode], this.nodes[ReceivingNode]);
        }

        public void a_real_transaction()
        {
            var transactionBuildContext = new TransactionBuildContext(
                    this.miningWalletAccountReference,
                    new List<Recipient>() { new Recipient() { Amount = this.transferAmount, ScriptPubKey = this.receiverAddress.ScriptPubKey } },
                    this.password)
            { MinConfirmations = this.maturity };
            this.transaction = this.nodes[PosNode].FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
            this.nodes[PosNode].FullNode.NodeService<WalletController>()
                .SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.blockWithTransactionId = this.nodes[PosNode].GenerateStratisWithMiner(1).Single();
            this.nodes[PosNode].GenerateStratisWithMiner(1);
            this.sharedSteps.WaitForNodeToSync(this.nodes[PosNode], this.nodes[ReceivingNode]);
        }

        private void calling_block_with_valid_hash_via_api_returns_block()
        {
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.response = this.httpClient.GetStringAsync($"{this.apiUri}api/BlockStore/block?Hash={this.blockWithTransactionId}&OutputJson=true").GetAwaiter().GetResult();
        }

        private void the_real_block_should_be_retrieved()
        {
            BlockModel blockResponse = JsonDataSerializer.Instance.Deserialize<BlockModel>(this.response);
            blockResponse.Hash.Should().Be(this.blockWithTransactionId.ToString());
        }

        private void the_block_should_contain_the_transaction()
        {
            BlockModel blockResponse = JsonDataSerializer.Instance.Deserialize<BlockModel>(this.response);
            blockResponse.Transactions.Should().Contain(this.transaction.ToHex(Network.StratisRegTest));
        }
    }
}