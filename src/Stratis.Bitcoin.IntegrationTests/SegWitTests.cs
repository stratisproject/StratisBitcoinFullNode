using System;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class SegWitTests
    {
        [Fact]
        public void TestSegwit_MinedOnCore_ActivatedOn_StratisNode()
        {
            // This test only verifies that the BIP9 machinery is operating correctly on the Stratis PoW node.
            // Since newer versions of Bitcoin Core have segwit always activated from genesis, there is no need to
            // perform the reverse version of this test. Much more important are the P2P and mempool tests for
            // segwit transactions.

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode coreNode = builder.CreateBitcoinCoreNode(version: "0.15.1");
                coreNode.Start();

                CoreNode stratisNode = builder.CreateStratisPowNode(KnownNetworks.RegTest).Start();

                RPCClient stratisNodeRpc = stratisNode.CreateRPCClient();
                RPCClient coreRpc = coreNode.CreateRPCClient();

                coreRpc.AddNode(stratisNode.Endpoint, false);
                stratisNodeRpc.AddNode(coreNode.Endpoint, false);

                // Core (in version 0.15.1) only mines segwit blocks above a certain height on regtest
                // See issue for more details https://github.com/stratisproject/StratisBitcoinFullNode/issues/1028
                BIP9DeploymentsParameters prevSegwitDeployment = KnownNetworks.RegTest.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit];
                KnownNetworks.RegTest.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit] = new BIP9DeploymentsParameters("Test", 1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp());

                try
                {
                    // Generate 450 blocks, block 431 will be segwit activated.
                    coreRpc.Generate(450);
                    var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token;
                    TestBase.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNode.CreateRPCClient().GetBestBlockHash(), cancellationToken: cancellationToken);

                    // Segwit activation on Bitcoin regtest.
                    // - On regtest deployment state changes every 144 blocks, the threshold for activating a rule is 108 blocks.
                    // Segwit deployment status should be:
                    // - Defined up to block 142.
                    // - Started at block 143 to block 286.
                    // - LockedIn 287 (as segwit should already be signaled in blocks).
                    // - Active at block 431.

                    var consensusLoop = stratisNode.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                    ThresholdState[] segwitDefinedState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(142));
                    ThresholdState[] segwitStartedState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(143));
                    ThresholdState[] segwitLockedInState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(287));
                    ThresholdState[] segwitActiveState = consensusLoop.NodeDeployments.BIP9.GetStates(stratisNode.FullNode.ChainIndexer.GetHeader(431));

                    // Check that segwit got activated at block 431.
                    Assert.Equal(ThresholdState.Defined, segwitDefinedState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Started, segwitStartedState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.LockedIn, segwitLockedInState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                    Assert.Equal(ThresholdState.Active, segwitActiveState.GetValue((int)BitcoinBIP9Deployments.Segwit));
                }
                finally
                {
                    KnownNetworks.RegTest.Consensus.BIP9Deployments[BitcoinBIP9Deployments.Segwit] = prevSegwitDeployment;
                }
            }
        }
    }
}
