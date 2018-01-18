using System;
using NBitcoin;
using NBitcoin.RPC;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SegWitTests
    {
        [Fact]
        public void TestSegwit_MinedOnCore_ActivatedOn_Stratisnode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(version: "0.15.1"))
            {
                CoreNode coreNode = builder.CreateNode(false);

                coreNode.ConfigParameters.AddOrReplace("debug", "1");
                coreNode.ConfigParameters.AddOrReplace("printtoconsole", "0");
                coreNode.Start();

                CoreNode stratisNode = builder.CreateStratisPowNode(true, fullNodeBuilder =>
                {
                    fullNodeBuilder
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .AddMining()
                        .UseWallet()
                        .UseApi()
                        .AddRPC();
                });

                WalletManager stratisNodeWallet = stratisNode.FullNode.NodeService<IWalletManager>() as WalletManager;
                stratisNodeWallet.CreateWallet("Test1", "alice1");

                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();
                RPCClient coreRpc = coreNode.CreateRPCClient();

                coreRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(coreNode.Endpoint, false);

                // core does not mine segwit blocks by default so we disable the segwit auto activation on regtest
                var prevSegwitDeployment = Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit];
                Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = new BIP9DeploymentsParameters(1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

                try
                {
                    // generate 450 blocks, block432 is a segwit block
                    coreRpc.Generate(450);

                    TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash());

                    // segwit activation on Bitcoin regtest
                    // - On regtest deployment state changes every 144 block, the threshold for activating a rule is 108 blocks.
                    // segwit should be:
                    // - Defined up to block 143
                    // - Started at block 144 to block 288 
                    // - LockedIn 288 (as segwit blocks should already be signaled)
                    // - Active at block 432 

                    IConsensusLoop consensusLoop = stratisNode.FullNode.NodeService<IConsensusLoop>();
                    ThresholdState[] segwitDefinedState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.Chain.GetBlock(142));
                    ThresholdState[] segwitStartedState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.Chain.GetBlock(143));
                    ThresholdState[] segwitLockedInState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.Chain.GetBlock(287));
                    ThresholdState[] segwitActiveState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.Chain.GetBlock(431));

                    // check that segwit is got activated at block 432
                    Assert.Equal(ThresholdState.Defined, segwitDefinedState.GetValue((int)BIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Started, segwitStartedState.GetValue((int)BIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.LockedIn, segwitLockedInState.GetValue((int)BIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Active, segwitActiveState.GetValue((int)BIP9Deployments.Segwit));
                }
                finally
                {
                    Network.RegTest.Consensus.BIP9Deployments[BIP9Deployments.Segwit] = prevSegwitDeployment;
                }
            }
        }

        public void TestSegwit_MinedOnStratisNode_ActivatedOn_Corenode()
        {
            // TODO: mine segwit onh a stratis node on the bitcoin network
            // write a tests that mines segwit blocks on the stratis node 
            // and signals them to a core not, then segwit will get activated on core
        }
    }
}
