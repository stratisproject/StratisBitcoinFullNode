using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class HeaderTimeTests
    {
        [Fact]
        public async Task HeaderInFutureIsntAcceptedButNoBanAsync()
        {
            // Create 2 nodes from 2 different builders, so they have different internal times.
            var network = new TestPoANetwork();
            PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this);
            PoANodeBuilder builder2 = PoANodeBuilder.CreatePoANodeBuilder(this);
            CoreNode node1 = builder.CreatePoANode(network, network.FederationKey1).Start();
            CoreNode node2 = builder2.CreatePoANode(network, network.FederationKey2).Start();

            // They can connect, they agree on genesis.
            TestHelper.Connect(node1, node2);

            // When one mines a block, his time will be pushed forwards by multiple TargetSpacings.
            await node1.MineBlocksAsync(1);
            long dateTime1 = node1.FullNode.NodeService<IDateTimeProvider>().GetAdjustedTimeAsUnixTimestamp();
            long dateTime2 = node2.FullNode.NodeService<IDateTimeProvider>().GetAdjustedTimeAsUnixTimestamp();
            Assert.True(dateTime1 > dateTime2 + network.ConsensusOptions.TargetSpacingSeconds);

            // Give the other node plenty of time such that he should have also synced if he thought the block was valid.
            Thread.Sleep(3000);

            // But he didn't sync - header was too far ahead.
            Assert.Equal(0, node2.FullNode.ChainIndexer.Height);

            // However the other node isn't disconnected or banned.
            Assert.True(node2.FullNode.ConnectionManager.ConnectedPeers.Any());
        }
    }
}
