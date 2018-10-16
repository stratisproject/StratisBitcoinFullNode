using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Networks;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeSyncTests
    {
        private readonly Network posNetwork;
        private readonly Network powNetwork;

        const string walletName = "myWallet";
        const string walletPassword = "123456";
        const string walletPassphrase = "123456";
        const string walletAccount = "account 0";

        public NodeSyncTests()
        {
            this.posNetwork = NetworkRegistration.Register(new StratisRegTestMaxReorg());
            this.powNetwork = KnownNetworks.RegTest;
        }

        private class StratisRegTestMaxReorg : StratisRegTest
        {
            public StratisRegTestMaxReorg()
            {
                this.Consensus = new NBitcoin.Consensus(
                consensusFactory: base.Consensus.ConsensusFactory,
                consensusOptions: base.Consensus.Options,
                coinType: 105,
                hashGenesisBlock: base.GenesisHash,
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: base.Consensus.BuriedDeployments,
                bip9Deployments: base.Consensus.BIP9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 10,
                defaultAssumeValid: null, // turn off assumevalid for regtest.
                maxMoney: long.MaxValue,
                coinbaseMaturity: 10,
                premineHeight: 2,
                premineReward: Money.Coins(98000000),
                proofOfWorkReward: Money.Coins(4),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: true,
                powNoRetargeting: true,
                powLimit: base.Consensus.PowLimit,
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 12500,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.COIN
            );
            }
        }

        [Fact]
        public void NodesCanConnectToEachOthers()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().Start();
                CoreNode node2 = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().Start();

                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);

                TestHelper.Connect(node1, node2);
                Assert.Single(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Single(node2.FullNode.ConnectionManager.ConnectedPeers);

                var behavior = node1.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.OfType<IConnectionManagerBehavior>().FirstOrDefault();
                Assert.False(behavior.AttachedPeer.Inbound);
                Assert.True(behavior.OneTry);
                behavior = node2.FullNode.ConnectionManager.ConnectedPeers.First().Behaviors.OfType<IConnectionManagerBehavior>().FirstOrDefault();
                Assert.True(behavior.AttachedPeer.Inbound);
                Assert.False(behavior.OneTry);
            }
        }

        [Retry]
        [Trait("Unstable", "True")]
        public void CanStratisSyncFromCore()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().Start();
                CoreNode coreNode = builder.CreateBitcoinCoreNode().Start();

                Block tip = coreNode.FindBlock(10).Last();
                TestHelper.ConnectAndSync(stratisNode, coreNode);

                TestHelper.Disconnect(stratisNode, coreNode);

                coreNode.FindBlock(10).Last();
                TestHelper.ConnectAndSync(coreNode, stratisNode);
            }
        }

        [Fact]
        public void CanStratisSyncFromStratis()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().Start();
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().Start();
                CoreNode coreCreateNode = builder.CreateBitcoinCoreNode().Start();

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                Block tip = coreCreateNode.FindBlock(5).Last();
                TestHelper.ConnectAndSync(stratisNode, coreCreateNode);

                TestHelper.WaitLoop(() => stratisNode.FullNode.ConsensusManager().Tip.Block.GetHash() == tip.GetHash());

                // Add a new stratis node which will download
                // the blocks using the GetData payload
                TestHelper.ConnectAndSync(stratisNodeSync, stratisNode);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.Block.GetHash() == tip.GetHash());
            }
        }

        [Fact]
        public void CanCoreSyncFromStratis()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().Start();
                CoreNode coreNodeSync = builder.CreateBitcoinCoreNode().Start();
                CoreNode coreCreateNode = builder.CreateBitcoinCoreNode().Start();

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                Block tip = coreCreateNode.FindBlock(5).Last();
                TestHelper.ConnectAndSync(stratisNode, coreCreateNode);
                TestHelper.WaitLoop(() => stratisNode.FullNode.ConsensusManager().Tip.Block.GetHash() == tip.GetHash());

                // add a new stratis node which will download
                // the blocks using the GetData payload
                TestHelper.ConnectAndSync(coreNodeSync, stratisNode);
            }
        }

        [Retry(2)]
        [Trait("Unstable", "True")]
        public void Given_NodesAreSynced_When_ABigReorgHappens_Then_TheReorgIsIgnored()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisMiner = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();
                CoreNode stratisSyncer = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().Start();
                CoreNode stratisReorg = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();

                TestHelper.MineBlocks(stratisMiner, 1);

                // Wait for block repo for block sync to work
                TestHelper.ConnectAndSync(stratisMiner, stratisReorg);
                TestHelper.ConnectAndSync(stratisMiner, stratisSyncer);

                // Create a reorg by mining on two different chains                
                TestHelper.Disconnect(stratisMiner, stratisReorg);
                TestHelper.Disconnect(stratisSyncer, stratisReorg);

                TestHelper.MineBlocks(stratisMiner, 11);
                TestHelper.MineBlocks(stratisReorg, 12);

                // make sure the nodes are actually on different chains.
                Assert.NotEqual(stratisMiner.FullNode.Chain.GetBlock(2).HashBlock, stratisReorg.FullNode.Chain.GetBlock(2).HashBlock);

                TestHelper.TriggerSync(stratisSyncer);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));

                // The hash before the reorg node is connected.
                uint256 hashBeforeReorg = stratisMiner.FullNode.Chain.Tip.HashBlock;

                // Connect the reorg chain
                TestHelper.Connect(stratisMiner, stratisReorg);
                TestHelper.Connect(stratisSyncer, stratisReorg);

                // Trigger nodes to sync
                TestHelper.TriggerSync(stratisMiner);
                TestHelper.TriggerSync(stratisReorg);
                TestHelper.TriggerSync(stratisSyncer);

                // Wait for the synced chain to get headers updated.
                TestHelper.WaitLoop(() => !stratisReorg.FullNode.ConnectionManager.ConnectedPeers.Any());

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReorg, stratisMiner) == false);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReorg, stratisSyncer) == false);

                // Check that a reorg did not happen.
                Assert.Equal(hashBeforeReorg, stratisSyncer.FullNode.Chain.Tip.HashBlock);
            }
        }

        /// <summary>
        /// This tests simulates scenario 2 from issue 636.
        /// <para>
        /// The test mines a block and roughly at the same time, but just after that, a new block at the same height
        /// arrives from the puller. Then another block comes from the puller extending the chain without the block we mined.
        /// </para>
        /// </summary>
        /// <seealso cref="https://github.com/stratisproject/StratisBitcoinFullNode/issues/636"/>
        [Retry]
        [Trait("Unstable", "True")]
        public void PullerVsMinerRaceCondition()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // This represents local node.
                CoreNode stratisMinerLocal = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();

                // This represents remote, which blocks are received by local node using its puller.
                CoreNode stratisMinerRemote = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();

                // Let's mine block Ap and Bp.
                TestHelper.MineBlocks(stratisMinerRemote, 2);

                // Wait for block repository for block sync to work.
                TestHelper.ConnectAndSync(stratisMinerLocal, stratisMinerRemote);

                // Now disconnect the peers and mine block C2p on remote.
                TestHelper.ConnectAndSync(stratisMinerLocal, stratisMinerRemote);

                // Mine block C2p.
                TestHelper.MineBlocks(stratisMinerRemote, 1);
                Thread.Sleep(2000);

                // Now reconnect nodes and mine block C1s before C2p arrives.
                TestHelper.ConnectAndSync(stratisMinerLocal, stratisMinerRemote);

                TestHelper.MineBlocks(stratisMinerLocal, 1);

                // Mine block Dp.
                uint256 dpHash = TestHelper.MineBlocks(stratisMinerRemote, 1).BlockHashes[0];

                // Now we wait until the local node's chain tip has correct hash of Dp.
                TestHelper.WaitLoop(() => stratisMinerLocal.FullNode.Chain.Tip.HashBlock.Equals(dpHash));

                // Then give it time to receive the block from the puller.
                Thread.Sleep(2500);

                // Check that local node accepted the Dp as consensus tip.
                Assert.Equal(stratisMinerLocal.FullNode.ChainBehaviorState.ConsensusTip.HashBlock, dpHash);
            }
        }

        /// <summary>
        /// This test simulates scenario from issue #862.
        /// <para>
        /// Connection scheme:
        /// Network - Node1 - MiningNode
        /// </para>
        /// </summary>
        [Retry]
        [Trait("Unstable", "True")]
        public void MiningNodeWithOneConnectionAlwaysSynced()
        {
            string testFolderPath = Path.Combine(this.GetType().Name, nameof(MiningNodeWithOneConnectionAlwaysSynced));

            using (NodeBuilder nodeBuilder = NodeBuilder.Create(testFolderPath))
            {
                CoreNode minerNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet().Start();
                CoreNode connectorNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet().Start();
                CoreNode firstNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet().Start();
                CoreNode secondNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet().Start();

                TestHelper.Connect(minerNode, connectorNode);
                TestHelper.Connect(connectorNode, firstNode);
                TestHelper.Connect(connectorNode, secondNode);
                TestHelper.Connect(firstNode, secondNode);

                List<CoreNode> nodes = new List<CoreNode> { minerNode, connectorNode, firstNode, secondNode };

                nodes.ForEach(n =>
                {
                    TestHelper.MineBlocks(n, 1);
                    TestHelper.WaitForNodeToSync(nodes.ToArray());
                });

                Assert.Equal(4, minerNode.FullNode.Chain.Height);

                // firstNode mines block 5.
                TestHelper.MineBlocks(firstNode, 1);

                // Wait until connector get the hash of network's block.
                while ((connectorNode.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != firstNode.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) ||
                       (firstNode.FullNode.ChainBehaviorState.ConsensusTip.Height == 5))
                    Thread.Sleep(1);

                Assert.Equal(connectorNode.FullNode.Chain.Tip.HashBlock, firstNode.FullNode.Chain.Tip.HashBlock);
                Assert.Equal(4, minerNode.FullNode.Chain.Tip.Height);
                Assert.Equal(5, connectorNode.FullNode.Chain.Tip.Height);

                // Miner mines block 6.
                TestHelper.MineBlocks(minerNode, 1);

                Assert.Equal(connectorNode.FullNode.Chain.Tip.HashBlock, firstNode.FullNode.Chain.Tip.HashBlock);
                Assert.Equal(6, minerNode.FullNode.Chain.Tip.Height);
                Assert.Equal(6, connectorNode.FullNode.Chain.Tip.Height);

                // connectorNode mines block 7.
                TestHelper.MineBlocks(connectorNode, 1);

                TestHelper.WaitForNodeToSync(nodes.ToArray());

                nodes.All(n => n.FullNode.Chain.Height == 7).Should().BeTrue(because: "all nodes have synced to chain height");

                Assert.Equal(firstNode.FullNode.Chain.Tip.HashBlock, minerNode.FullNode.Chain.Tip.HashBlock);
            }
        }
    }
}
