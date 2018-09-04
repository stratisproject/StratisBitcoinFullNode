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
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeSyncTests
    {
        private readonly Network posNetwork;
        private readonly Network powNetwork;

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
                CoreNode node1 = builder.CreateStratisPowNode(this.powNetwork);
                CoreNode node2 = builder.CreateStratisPowNode(this.powNetwork);
                builder.StartAll();
                node1.NotInIBD();
                node2.NotInIBD();
                Assert.Empty(node1.FullNode.ConnectionManager.ConnectedPeers);
                Assert.Empty(node2.FullNode.ConnectionManager.ConnectedPeers);
                RPCClient rpc1 = node1.CreateRPCClient();
                rpc1.AddNode(node2.Endpoint, true);
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

        [Fact]
        public void CanStratisSyncFromCore()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.powNetwork);
                CoreNode coreNode = builder.CreateBitcoinCoreNode();
                builder.StartAll();

                stratisNode.NotInIBD();

                Block tip = coreNode.FindBlock(10).Last();
                RPCClient stratisNodeRpcClient = stratisNode.CreateRPCClient();
                stratisNodeRpcClient.AddNode(coreNode.Endpoint, true);
                RPCClient coreNodeRpcClient = coreNode.CreateRPCClient();
                TestHelper.WaitLoop(() => stratisNodeRpcClient.GetBestBlockHash() == coreNodeRpcClient.GetBestBlockHash());
                uint256 bestBlockHash = stratisNodeRpcClient.GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                stratisNodeRpcClient.RemoveNode(coreNode.Endpoint);
                TestHelper.WaitLoop(() => coreNodeRpcClient.GetPeersInfo()
                    .All(pi => pi.Address.MapToIpv6().ToString() != coreNode.Endpoint.MapToIpv6().ToString()));

                tip = coreNode.FindBlock(10).Last();
                coreNodeRpcClient.AddNode(stratisNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNodeRpcClient.GetBestBlockHash() == coreNodeRpcClient.GetBestBlockHash());
                bestBlockHash = stratisNodeRpcClient.GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanStratisSyncFromStratis()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.powNetwork);
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.powNetwork);
                CoreNode coreCreateNode = builder.CreateBitcoinCoreNode();
                builder.StartAll();

                stratisNode.NotInIBD();
                stratisNodeSync.NotInIBD();

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                Block tip = coreCreateNode.FindBlock(5).Last();
                stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                uint256 bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new stratis node which will download
                // the blocks using the GetData payload
                stratisNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = stratisNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void CanCoreSyncFromStratis()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.powNetwork);
                CoreNode coreNodeSync = builder.CreateBitcoinCoreNode();
                CoreNode coreCreateNode = builder.CreateBitcoinCoreNode();
                builder.StartAll();

                stratisNode.NotInIBD();

                // first seed a core node with blocks and sync them to a stratis node
                // and wait till the stratis node is fully synced
                Block tip = coreCreateNode.FindBlock(5).Last();
                stratisNode.CreateRPCClient().AddNode(coreCreateNode.Endpoint, true);
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreCreateNode.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode.FullNode.GetBlockStoreTip().HashBlock == stratisNode.FullNode.Chain.Tip.HashBlock);

                uint256 bestBlockHash = stratisNode.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);

                // add a new stratis node which will download
                // the blocks using the GetData payload
                coreNodeSync.CreateRPCClient().AddNode(stratisNode.Endpoint, true);

                // wait for download and assert
                TestHelper.WaitLoop(() => stratisNode.CreateRPCClient().GetBestBlockHash() == coreNodeSync.CreateRPCClient().GetBestBlockHash());
                bestBlockHash = coreNodeSync.CreateRPCClient().GetBestBlockHash();
                Assert.Equal(tip.GetHash(), bestBlockHash);
            }
        }

        [Fact]
        public void Given_NodesAreSynced_When_ABigReorgHappens_Then_TheReorgIsIgnored()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisMiner = builder.CreateStratisPosNode(this.posNetwork);
                CoreNode stratisSyncer = builder.CreateStratisPosNode(this.posNetwork);
                CoreNode stratisReorg = builder.CreateStratisPosNode(this.posNetwork);

                builder.StartAll();
                stratisMiner.NotInIBD().WithWallet();
                stratisSyncer.NotInIBD().WithWallet();
                stratisReorg.NotInIBD().WithWallet();

                stratisMiner.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisMiner.FullNode.Network));
                stratisReorg.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisReorg.FullNode.Network));

                stratisMiner.GenerateStratisWithMiner(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisMiner));
                stratisMiner.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                stratisMiner.CreateRPCClient().AddNode(stratisSyncer.Endpoint, true);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisReorg));

                // create a reorg by mining on two different chains
                // ================================================

                stratisMiner.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                stratisSyncer.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(stratisReorg));

                stratisMiner.GenerateStratisWithMiner(11);
                stratisReorg.GenerateStratisWithMiner(12);

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisMiner));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisReorg));

                // make sure the nodes are actually on different chains.
                Assert.NotEqual(stratisMiner.FullNode.Chain.GetBlock(2).HashBlock, stratisReorg.FullNode.Chain.GetBlock(2).HashBlock);

                TestHelper.TriggerSync(stratisSyncer);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));

                // The hash before the reorg node is connected.
                uint256 hashBeforeReorg = stratisMiner.FullNode.Chain.Tip.HashBlock;

                // connect the reorg chain
                stratisMiner.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                stratisSyncer.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);

                // trigger nodes to sync
                TestHelper.TriggerSync(stratisMiner);
                TestHelper.TriggerSync(stratisReorg);
                TestHelper.TriggerSync(stratisSyncer);

                // wait for the synced chain to get headers updated.
                TestHelper.WaitLoop(() => !stratisReorg.FullNode.ConnectionManager.ConnectedPeers.Any());

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMiner, stratisSyncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReorg, stratisMiner) == false);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReorg, stratisSyncer) == false);

                // check that a reorg did not happen.
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
        [Fact]
        public void PullerVsMinerRaceCondition()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // This represents local node.
                CoreNode stratisMinerLocal = builder.CreateStratisPosNode(this.posNetwork);

                // This represents remote, which blocks are received by local node using its puller.
                CoreNode stratisMinerRemote = builder.CreateStratisPosNode(this.posNetwork);

                builder.StartAll();
                stratisMinerLocal.NotInIBD().WithWallet();
                stratisMinerRemote.NotInIBD().WithWallet();

                stratisMinerLocal.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisMinerLocal.FullNode.Network));
                stratisMinerRemote.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisMinerRemote.FullNode.Network));

                // Let's mine block Ap and Bp.
                stratisMinerRemote.GenerateStratisWithMiner(2);

                // Wait for block repository for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisMinerRemote));
                stratisMinerLocal.CreateRPCClient().AddNode(stratisMinerRemote.Endpoint, true);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisMinerLocal, stratisMinerRemote));

                // Now disconnect the peers and mine block C2p on remote.
                stratisMinerLocal.CreateRPCClient().RemoveNode(stratisMinerRemote.Endpoint);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(stratisMinerRemote));

                // Mine block C2p.
                stratisMinerRemote.GenerateStratisWithMiner(1);
                Thread.Sleep(2000);

                // Now reconnect nodes and mine block C1s before C2p arrives.
                stratisMinerLocal.CreateRPCClient().AddNode(stratisMinerRemote.Endpoint, true);
                stratisMinerLocal.GenerateStratisWithMiner(1);

                // Mine block Dp.
                uint256 dpHash = stratisMinerRemote.GenerateStratisWithMiner(1)[0];

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
        [Fact]
        public void MiningNodeWithOneConnectionAlwaysSynced()
        {
            const string walletName = "dummyWallet";
            const string walletPassword = "dummyPassword";
            const string walletPassphrase = "dummyPassphrase";

            var sharedSteps = new SharedSteps();
            string testFolderPath = Path.Combine(this.GetType().Name, nameof(MiningNodeWithOneConnectionAlwaysSynced));
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(testFolderPath))
            {
                CoreNode minerNode = nodeBuilder.CreateStratisPowNode(this.powNetwork);
                minerNode.Start();
                minerNode.NotInIBD();
                minerNode.FullNode.WalletManager().CreateWallet(walletPassword, walletName, walletPassphrase);

                CoreNode connectorNode = nodeBuilder.CreateStratisPowNode(this.powNetwork);
                connectorNode.Start();
                connectorNode.NotInIBD();
                connectorNode.FullNode.WalletManager().CreateWallet(walletPassword, walletName, walletPassphrase);

                CoreNode firstNode = nodeBuilder.CreateStratisPowNode(this.powNetwork);
                firstNode.Start();
                firstNode.NotInIBD();
                firstNode.FullNode.WalletManager().CreateWallet(walletPassword, walletName, walletPassphrase);

                CoreNode secondNode = nodeBuilder.CreateStratisPowNode(this.powNetwork);
                secondNode.Start();
                secondNode.NotInIBD();
                secondNode.FullNode.WalletManager().CreateWallet(walletPassword, walletName, walletPassphrase);

                TestHelper.ConnectAndSync(minerNode, connectorNode);
                TestHelper.ConnectAndSync(connectorNode, firstNode);
                TestHelper.ConnectAndSync(connectorNode, secondNode);
                TestHelper.ConnectAndSync(firstNode, secondNode);

                List<CoreNode> nodes = new List<CoreNode> { minerNode, connectorNode, firstNode, secondNode };
                sharedSteps.WaitForNodeToSync(nodes.ToArray());

                nodes.ForEach(n =>
                {
                    sharedSteps.MineBlocks(1, n, "account 0", walletName, walletPassword);
                    sharedSteps.WaitForNodeToSync(nodes.ToArray());
                });

                int networkHeight = minerNode.FullNode.Chain.Height;
                Assert.Equal(networkHeight, nodes.Count);

                // Random node on network generates a block.
                firstNode.GenerateStratisWithMiner(1);

                // Wait until connector get the hash of network's block.
                while ((connectorNode.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != firstNode.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) ||
                       (firstNode.FullNode.ChainBehaviorState.ConsensusTip.Height == networkHeight))
                    Thread.Sleep(1);

                Assert.Equal(connectorNode.FullNode.Chain.Tip.HashBlock, firstNode.FullNode.Chain.Tip.HashBlock);
                Assert.Equal(minerNode.FullNode.Chain.Tip.Height, networkHeight);
                Assert.Equal(connectorNode.FullNode.Chain.Tip.Height, networkHeight + 1);

                // Miner mines the block.
                minerNode.GenerateStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(minerNode));

                networkHeight++;

                Assert.Equal(connectorNode.FullNode.Chain.Tip.HashBlock, firstNode.FullNode.Chain.Tip.HashBlock);
                Assert.Equal(minerNode.FullNode.Chain.Tip.Height, networkHeight);
                Assert.Equal(connectorNode.FullNode.Chain.Tip.Height, networkHeight);

                connectorNode.GenerateStratisWithMiner(1);
                networkHeight++;

                sharedSteps.WaitForNodeToSync(nodes.ToArray());

                nodes.All(n => n.FullNode.Chain.Height == networkHeight).Should()
                    .BeTrue(because: "all nodes have synced to chain height");

                Assert.Equal(firstNode.FullNode.Chain.Tip.HashBlock, minerNode.FullNode.Chain.Tip.HashBlock);
            }
        }
    }
}
