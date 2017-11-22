using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PeerBanningTest
    {
        [Fact]
        public async Task GivenANodeIsSyncedAndNotConnected_WhenAPeerSendsABadBlock_ThePeerGetsBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.GivenANodeIsSyncedAndNotConnected_WhenAPeerSendsABadBlock_ThePeerGetsBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peer = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns((Node)null);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peer });

            Assert.True(context.PeerBanning.IsBanned(peer));
        }

        [Fact]
        public async Task GivenANodeIsSyncedAndConnected_WhenAPeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.GivenANodeIsSyncedAndConnected_WhenAPeerSendsABadBlockAndPeerIsWhitelisted_ThePeerIsNotBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peer = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory) { Whitelisted = true };
            var node = new Node();
            node.Behaviors.Add(connectionManagerBehavior);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(node);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peer });

            Assert.False(context.PeerBanning.IsBanned(peer));
        }

        [Fact]
        public async Task GivenANodeIsSyncedAndConnected_WhenAPeerSendsABadBlockAndPeerIsNotWhitelisted_ThePeerGetsBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.GivenANodeIsSyncedAndConnected_WhenAPeerSendsABadBlockAndPeerIsNotWhitelisted_ThePeerGetsBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, dataDir);
            var peer = new IPEndPoint(IPAddress.Parse("1.2.3.4"), context.Network.DefaultPort);

            var connectionManagerBehavior = new ConnectionManagerBehavior(false, context.ConnectionManager, context.LoggerFactory) { Whitelisted = false };
            var node = new Node();
            node.Behaviors.Add(connectionManagerBehavior);
            context.MockReadOnlyNodesCollection.Setup(s => s.FindByEndpoint(It.IsAny<IPEndPoint>())).Returns(node);

            var blocks = await TestChainFactory.MineBlocksAsync(context, 2, new Key().ScriptPubKey);
            // create a new block that breaks consensus.
            var block = blocks.First();
            block.Header.HashPrevBlock = context.Chain.Tip.HashBlock;
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = peer });

            Assert.True(context.PeerBanning.IsBanned(peer));
        }
    }
}
