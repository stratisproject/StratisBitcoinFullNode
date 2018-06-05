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
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;
using Xunit.Sdk;

namespace EnigmaChain.IntegrationTests
{
    [Collection("SidechainIdentifierTests")]
    public class Generated_Node_Shall
    {
        [Fact(Skip = "Fails after nuget update")]
        public async Task Mine_Premine()
        {
            using (var nodeBuilder = NodeBuilder.Create())
            {
                var sidechainNode = nodeBuilder.CreatePosSidechainNode("enigma", false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi()
                        .AddRPC();
                });

                //command line args and start
                sidechainNode.ConfigParameters.Add("apiuri", "http://localhost:38771");
                sidechainNode.ConfigParameters.AddOrReplace("port", "38001");
                sidechainNode.ConfigParameters.Add("addnode", "127.0.0.1:38000");
                sidechainNode.Start();

                //create wallet
                var mnemonic = sidechainNode.FullNode.WalletManager().CreateWallet("123456", "wallet");
                mnemonic.Words.Length.Should().Be(12);

                //mine three blocks using RPC
                RPCClient rpc = sidechainNode.CreateRPCClient();
                rpc.SendCommand(NBitcoin.RPC.RPCOperations.generate, 3);
                rpc.GetBlockCount().Should().Be(3);

                //test the wallet balance
                var total = sidechainNode.FullNode.WalletManager().GetSpendableTransactionsInWallet("wallet").Sum(s => s.Transaction.Amount);
                total.Should().Be(9800000800000000L);
            }
        }
    }
}
