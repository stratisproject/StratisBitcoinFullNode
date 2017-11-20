using System.IO;
using System.Net;
using System.Threading.Tasks;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PeerBanningTest
    {
        [Fact]
        public async Task GivenANodeIsSynced_WhenAPeerSendsABadBlock_ThePeerGetBanned_Async()
        {
            string dataDir = Path.Combine("TestData", nameof(PeerBanningTest), nameof(this.GivenANodeIsSynced_WhenAPeerSendsABadBlock_ThePeerGetBanned_Async));
            Directory.CreateDirectory(dataDir);

            TestChainContext context = await TestChainFactory.CreateAsync(Network.RegTest, new Key().ScriptPubKey, dataDir);

            // create a new block that breaks consensus.
            var block = new Block();
            var peer = new IPEndPoint(IPAddress.Parse("1.2.3.4"), Network.TestNet.DefaultPort);
            await context.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = block, Peer = new IPEndPoint(IPAddress.Parse(""), Network.TestNet.DefaultPort) });

            context.PeerBanning.IsBanned(peer);

        }

    }
}
