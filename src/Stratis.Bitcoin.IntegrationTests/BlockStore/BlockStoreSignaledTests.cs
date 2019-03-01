using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    /// <summary>
    /// Used for recording messages coming into a test node. Does not respond to them in any way.
    /// </summary>
    internal class TestBehavior : NetworkPeerBehavior
    {
        public Dictionary<string, List<IncomingMessage>> receivedMessageTracker = new Dictionary<string, List<IncomingMessage>>();

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            try
            {
                await this.ProcessMessageAsync(peer, message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!this.receivedMessageTracker.ContainsKey(message.Message.Payload.Command))
                this.receivedMessageTracker[message.Message.Payload.Command] = new List<IncomingMessage>();

            this.receivedMessageTracker[message.Message.Payload.Command].Add(message);
        }

        public override object Clone()
        {
            var res = new TestBehavior();

            return res;
        }
    }

    public class BlockStoreSignaledTests
    {
        protected readonly ILoggerFactory loggerFactory;
        private readonly Network network;

        public BlockStoreSignaledTests()
        {
            this.loggerFactory = new LoggerFactory();

            this.network = new BitcoinRegTest();
        }

        [Fact]
        public void CheckBlocksAnnounced_AndQueueEmptiesOverTime()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).Start();

                // Change the second node's list of default behaviours include the test behaviour in it.
                // We leave the other behaviors alone for this test because we want to see what messages the node gets under normal operation.
                IConnectionManager node1ConnectionManager = stratisNode1.FullNode.NodeService<IConnectionManager>();
                node1ConnectionManager.Parameters.TemplateBehaviors.Add(new TestBehavior());

                // Connect node1 to initial node.
                TestHelper.Connect(stratisNode1, stratisNodeSync);

                INetworkPeer connectedPeer = node1ConnectionManager.ConnectedPeers.FindByEndpoint(stratisNodeSync.Endpoint);
                TestBehavior testBehavior = connectedPeer.Behavior<TestBehavior>();

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode1, stratisNodeSync));

                HashSet<uint256> advertised = new HashSet<uint256>();

                // Check to see that all blocks got advertised to node1 via the "headers" payload.
                foreach (IncomingMessage message in testBehavior.receivedMessageTracker["headers"])
                {
                    if (message.Message.Payload is HeadersPayload)
                        foreach (BlockHeader header in ((HeadersPayload)message.Message.Payload).Headers)
                            advertised.Add(header.GetHash());
                }

                foreach (ChainedHeader chainedHeader in stratisNodeSync.FullNode.Chain.EnumerateToTip(this.network.GenesisHash))
                    if ((!advertised.Contains(chainedHeader.HashBlock)) && (!(chainedHeader.HashBlock == this.network.GenesisHash)))
                        throw new Exception($"An expected block was not advertised to peer: {chainedHeader.HashBlock}");

                // Check current state of announce queue
                BlockStoreSignaled blockStoreSignaled = stratisNodeSync.FullNode.NodeService<BlockStoreSignaled>();

                AsyncQueue<ChainedHeader> blocksToAnnounce = (AsyncQueue<ChainedHeader>)blockStoreSignaled.GetMemberValue("blocksToAnnounce");
                Queue<ChainedHeader> queueItems = (Queue<ChainedHeader>)blocksToAnnounce.GetMemberValue("items");

                TestHelper.WaitLoop(() => queueItems.Count == 0);
            }
        }

        [Fact]
        public void CheckBlocksAnnounced_AndQueueEmptiesOverTime_ForMultiplePeers_WhenOneIsDisconnected()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();

                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).Start();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.network).Start();
                CoreNode stratisNode3 = builder.CreateStratisPowNode(this.network).Start();

                // Change the other nodes' lists of default behaviours include the test behaviour in it.
                // We leave the other behaviors alone for this test because we want to see what messages the node gets under normal operation.
                IConnectionManager node1ConnectionManager = stratisNode1.FullNode.NodeService<IConnectionManager>();
                node1ConnectionManager.Parameters.TemplateBehaviors.Add(new TestBehavior());

                IConnectionManager node2ConnectionManager = stratisNode2.FullNode.NodeService<IConnectionManager>();
                node2ConnectionManager.Parameters.TemplateBehaviors.Add(new TestBehavior());

                // Connect other nodes to initial node.
                TestHelper.Connect(stratisNode1, stratisNodeSync);
                TestHelper.Connect(stratisNode2, stratisNodeSync);
                TestHelper.Connect(stratisNode3, stratisNodeSync);

                // Make node3 unable to respond to anything, effectively disconnecting it.
                IConnectionManager node3ConnectionManager = stratisNode3.FullNode.NodeService<IConnectionManager>();
                node3ConnectionManager.Parameters.TemplateBehaviors.Clear();
                node3ConnectionManager.Parameters.TemplateBehaviors.Add(new TestBehavior());

                INetworkPeer connectedPeer1 = node1ConnectionManager.ConnectedPeers.FindByEndpoint(stratisNodeSync.Endpoint);
                TestBehavior testBehavior1 = connectedPeer1.Behavior<TestBehavior>();

                INetworkPeer connectedPeer2 = node2ConnectionManager.ConnectedPeers.FindByEndpoint(stratisNodeSync.Endpoint);
                TestBehavior testBehavior2 = connectedPeer2.Behavior<TestBehavior>();

                INetworkPeer connectedPeer3 = node3ConnectionManager.ConnectedPeers.FindByEndpoint(stratisNodeSync.Endpoint);
                TestBehavior testBehavior3 = connectedPeer3.Behavior<TestBehavior>();

                // If the announce queue is not getting stalled, the other 2 nodes should sync properly.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode1, stratisNodeSync));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode2, stratisNodeSync));

                HashSet<uint256> advertised = new HashSet<uint256>();

                // Check to see that all blocks got advertised to node1 via the "headers" payload.
                foreach (IncomingMessage message in testBehavior1.receivedMessageTracker["headers"])
                {
                    if (message.Message.Payload is HeadersPayload)
                        foreach (BlockHeader header in ((HeadersPayload)message.Message.Payload).Headers)
                            advertised.Add(header.GetHash());
                }

                foreach (ChainedHeader chainedHeader in stratisNodeSync.FullNode.Chain.EnumerateToTip(this.network.GenesisHash))
                    if ((!advertised.Contains(chainedHeader.HashBlock)) && (!(chainedHeader.HashBlock == this.network.GenesisHash)))
                        throw new Exception($"An expected block was not advertised to peer 1: {chainedHeader.HashBlock}");

                advertised.Clear();

                // Check to see that all blocks got advertised to node1 via the "headers" payload.
                foreach (IncomingMessage message in testBehavior2.receivedMessageTracker["headers"])
                {
                    if (message.Message.Payload is HeadersPayload)
                        foreach (BlockHeader header in ((HeadersPayload)message.Message.Payload).Headers)
                            advertised.Add(header.GetHash());
                }

                foreach (ChainedHeader chainedHeader in stratisNodeSync.FullNode.Chain.EnumerateToTip(this.network.GenesisHash))
                    if ((!advertised.Contains(chainedHeader.HashBlock)) && (!(chainedHeader.HashBlock == this.network.GenesisHash)))
                        throw new Exception($"An expected block was not advertised to peer 2: {chainedHeader.HashBlock}");

                // Check current state of announce queue.
                BlockStoreSignaled blockStoreSignaled = stratisNodeSync.FullNode.NodeService<BlockStoreSignaled>();

                AsyncQueue<ChainedHeader> blocksToAnnounce = (AsyncQueue<ChainedHeader>)blockStoreSignaled.GetMemberValue("blocksToAnnounce");
                Queue<ChainedHeader> queueItems = (Queue<ChainedHeader>)blocksToAnnounce.GetMemberValue("items");

                // It should still eventually empty despite not being able to communicate with node3.
                TestHelper.WaitLoop(() => queueItems.Count == 0);
            }
        }

        [Fact]
        public void QueueEmpties_WithNoPeersConnected()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                BlockStoreSignaled blockStoreSignaled = stratisNodeSync.FullNode.NodeService<BlockStoreSignaled>();

                AsyncQueue<ChainedHeader> blocksToAnnounce = (AsyncQueue<ChainedHeader>)blockStoreSignaled.GetMemberValue("blocksToAnnounce");

                Queue<ChainedHeader> queueItems = (Queue<ChainedHeader>)blocksToAnnounce.GetMemberValue("items");

                // Announce queue length should drop to zero once the announce batch timer elapses at the latest.
                // Most likely it will be cleared almost instantly as the blocks getting mined are all tips.
                TestHelper.WaitLoop(() => queueItems.Count == 0);
            }
        }

        [Fact]
        public void MustNotAnnounceABlock_WhenNotInBestChain()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10NoWallet).Start();

                // Store block 1 of chain0 for later usage
                ChainedHeader firstBlock = null;
                foreach (ChainedHeader chainedHeader in stratisNodeSync.FullNode.Chain.EnumerateToTip(this.network.GenesisHash))
                {
                    if (chainedHeader.Height == 1)
                    {
                        firstBlock = chainedHeader;
                    }
                }

                Assert.NotNull(firstBlock);

                // Mine longer chain1 using node1
                TestHelper.MineBlocks(stratisNode1, 15);

                IConnectionManager node1ConnectionManager = stratisNode1.FullNode.NodeService<IConnectionManager>();
                node1ConnectionManager.Parameters.TemplateBehaviors.Add(new TestBehavior());

                IConnectionManager node2ConnectionManager = stratisNode2.FullNode.NodeService<IConnectionManager>();
                node2ConnectionManager.Parameters.TemplateBehaviors.Add(new TestBehavior());

                // Connect node0 and node1
                TestHelper.Connect(stratisNode1, stratisNodeSync);

                INetworkPeer connectedPeer = node1ConnectionManager.ConnectedPeers.FindByEndpoint(stratisNodeSync.Endpoint);
                TestBehavior testBehavior = connectedPeer.Behavior<TestBehavior>();

                // We expect that node0 will abandon the 10 block chain and use the 15 block chain from node1
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode1, stratisNodeSync));

                // Connect all nodes together
                TestHelper.Connect(stratisNode2, stratisNodeSync);
                TestHelper.Connect(stratisNode1, stratisNode2);

                INetworkPeer connectedPeer2 = node2ConnectionManager.ConnectedPeers.FindByEndpoint(stratisNodeSync.Endpoint);
                TestBehavior testBehavior2 = connectedPeer2.Behavior<TestBehavior>();

                // Wait for node2 to sync; it should have the 15 block chain
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode2, stratisNodeSync));

                // Insert block 1 from chain0 into node1's announce queue
                BlockStoreSignaled node1BlockStoreSignaled = stratisNode1.FullNode.NodeService<BlockStoreSignaled>();

                AsyncQueue<ChainedHeader> node1BlocksToAnnounce = (AsyncQueue<ChainedHeader>)node1BlockStoreSignaled.GetMemberValue("blocksToAnnounce");

                Queue<ChainedHeader> node1QueueItems = (Queue<ChainedHeader>)node1BlocksToAnnounce.GetMemberValue("items");

                TestHelper.WaitLoop(() => node1QueueItems.Count == 0);

                // Check that node2 does not have block 1 in test behaviour advertised list
                foreach (IncomingMessage message in testBehavior2.receivedMessageTracker["headers"])
                {
                    if (message.Message.Payload is HeadersPayload)
                    {
                        foreach (BlockHeader header in ((HeadersPayload)message.Message.Payload).Headers)
                        {
                            if (header.GetHash() == firstBlock.Header.GetHash())
                            {
                                throw new Exception("Should not have received payload announcing block from wrong chain");
                            }
                        }
                    }
                }
            }
        }
    }
}
