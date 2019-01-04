using System;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;
using System.Linq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using NBitcoin.Protocol;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Api;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ProvenHeaderTests
    {

        /// <summary>
        /// Prevent network being matched by name and replaced with a different network
        /// in the <see cref="Configuration.NodeSettings" /> constructor.
        /// </summary>
        public class StratisOverrideRegTest : StratisRegTest
        {
            public StratisOverrideRegTest(string name = null) : base()
            {
                this.Name = name ?? Guid.NewGuid().ToString();
            }
        }


        public CoreNode CreateNode(NodeBuilder nodeBuilder, string agent, ProtocolVersion version = ProtocolVersion.ALT_PROTOCOL_VERSION, NodeConfigParameters configParameters = null)
        {
            var callback = new Action<IFullNodeBuilder>(builder => builder
                .UseBlockStore()
                .UsePosConsensus()
                .UseMempool()
                .AddRPC()
                .UseApi()
                .UseTestChainedHeaderTree()
                .MockIBD()
                );

            return nodeBuilder.CreateCustomNode(callback, new StratisOverrideRegTest(), ProtocolVersion.PROVEN_HEADER_VERSION, agent: agent, configParameters: configParameters);
        }

        /// <summary>
        /// Tests that a slot is reserved for at least one PH enabled peer.
        /// </summary>
        [Fact(Skip = "WIP")]
        public void LegacyNodesConnectsToProvenHeaderEnabledNode_AndLastOneIsDisconnectedToReserveSlot()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this).WithLogsEnabled())
            {
                // Create separate network parameters for this test.
                CoreNode phEnabledNode = this.CreateNode(builder, "ph-enabled", ProtocolVersion.PROVEN_HEADER_VERSION, new NodeConfigParameters { { "maxoutboundconnections", "3" } }).Start();
                CoreNode legacyNode1 = this.CreateNode(builder, "legacy1", ProtocolVersion.ALT_PROTOCOL_VERSION).Start();
                CoreNode legacyNode2 = this.CreateNode(builder, "legacy2", ProtocolVersion.ALT_PROTOCOL_VERSION).Start();
                CoreNode legacyNode3 = this.CreateNode(builder, "legacy3", ProtocolVersion.ALT_PROTOCOL_VERSION).Start();

                TestHelper.Connect(phEnabledNode, legacyNode1);
                TestHelper.Connect(phEnabledNode, legacyNode2);
                TestHelper.Connect(phEnabledNode, legacyNode3);

                // TODO: ProvenHeadersReservedSlotsBehavior kicks in only during peer discovery, so it doesn't trigger when we have an inbound connection or
                // when we are using addnode/connect.
                // We need to configure a peers.json file or mock the PeerDiscovery to let phEnabledNode try to connect to legacyNode1, legacyNode2 and legacyNode3
                // With a maxoutboundconnections = 3, we expect the 3rd peer being disconnected to reserve a slot for a ph enabled node.

                // Assert.Equal(phEnabledNode.FullNode.ConnectionManager.ConnectedPeers.Count() == 2);
            }
        }
    }
}
