using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;
using Stratis.Bitcoin.Base.Deployments;
using NBitcoin.RPC;
using static NBitcoin.Consensus;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SegWitTests
    {
        [Fact]
        public void TestSegwitActivation()
        {
            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                CoreNode coreNode = builder.CreateNode(false);

                coreNode.ConfigParameters.AddOrReplace("debug", "1");
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");
                coreNode.Start();

                CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .AddMining()
                        .UseWallet()
                        .UseWatchOnlyWallet()
                        .UseApi()
                        .AddRPC();
                });

                WalletManager wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
                wm1.CreateWallet("Test1", "alice1");

                RPCClient rpc1 = node1.CreateRPCClient();
                RPCClient coreRpc = coreNode.CreateRPCClient();

                coreRpc.AddNode(node1.Endpoint, false);
                rpc1.AddNode(coreNode.Endpoint, false);

                coreRpc.Generate(450);

                BIP9DeploymentsArray bip9Constants = node1.FullNode.Network.Consensus.BIP9Deployments;
                ConsensusLoop consensusLoop = node1.FullNode.NodeService<ConsensusLoop>();
                ThresholdState[] bip9State = consensusLoop.NodeDeployments.BIP9.GetStates(node1.FullNode.Chain.Tip.Previous);

                Money amount = new Money(5.0m, MoneyUnit.BTC);
                HdAddress destination1 = wm1.GetUnusedAddress(new WalletAccountReference("alice1", "account 0"));

                coreRpc.SendToAddress(BitcoinAddress.Create(destination1.Address, Network.RegTest), amount);

                coreRpc.Generate(1);

                Assert.Equal(ThresholdState.Active, bip9State.GetValue((int)BIP9Deployments.Segwit));
            }
        }
    }
}
