using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using NBitcoin.RPC;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Xunit;
using System.Linq;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Sidechains.Features.BlockchainGeneration;
using Stratis.Sidechains.Features.BlockchainGeneration.Network;
using Xunit.Sdk;

namespace EnigmaChain.IntegrationTests
{
    [Collection("SidechainIdentifierTests")]
    public class Generated_Node_Shall
    {
        [Fact(Skip = "waiting for mining changes on the full node")]
        //TODO this needs to be fixed, waiting for changes on the full node to get
        //premine reward on POW
        public async Task Mine_Premine()
        {
            using (var nodeBuilder = NodeBuilder.Create(this))
            {
                var sidechainNode = nodeBuilder.CreateCustomNode(false, fullNodeBuilder =>
                //var sidechainNode = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddMining()
                        .SubstituteDateTimeProviderFor<MiningFeature>()
                        .UseApi()
                        .AddRPC();
                }, SidechainNetwork.SidechainRegTest);

                //command line args and start
                sidechainNode.ConfigParameters.Add("apiuri", "http://localhost:38771");
                sidechainNode.ConfigParameters.AddOrReplace("port", "38001");
                sidechainNode.ConfigParameters.Add("addnode", "127.0.0.1:38000");
                sidechainNode.Start();

                sidechainNode.FullNode.Chain.Tip.Height.Should().Be(0);

                //create wallet
                var walletManager = sidechainNode.FullNode.WalletManager();
                var mnemonic = walletManager.CreateWallet("123456", "wallet");
                mnemonic.Words.Length.Should().Be(12);

                var powMinting = sidechainNode.FullNode.NodeService<IPowMining>();

                var walletAccountReference = new WalletAccountReference("wallet", "account 0");
                var bitcoinAddress = walletManager.GetUnusedAddress(walletAccountReference);

                powMinting.GenerateBlocks(new ReserveScript(bitcoinAddress.ScriptPubKey), 3UL, int.MaxValue);

                sidechainNode.FullNode.Chain.Tip.Height.Should().Be(3);

                //mine three blocks using RPC
                //RPCClient rpc = sidechainNode.CreateRPCClient();
                //sidechainNode.FullNode.Services.ServiceProvider<IPowMining>;
                //rpc.SendCommand(NBitcoin.RPC.RPCOperations.generate, 10);
                //rpc.GetBlockCount().Should().Be(10);

                //test the wallet balance
                var total = walletManager.GetSpendableTransactionsInWallet("wallet").Sum(s => s.Transaction.Amount);
                total.Should().Be(9800000800000000L);
            }
        }
    }
}
