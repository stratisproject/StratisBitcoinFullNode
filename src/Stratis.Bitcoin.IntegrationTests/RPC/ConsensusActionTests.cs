using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class ConsensusActionTests : BaseRPCControllerTest
    {
        [Fact]
        public void CanCall_GetBestBlockHash()
        {
            string dir = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.Services.ServiceProvider.GetService<ConsensusController>();

            uint256 result = controller.GetBestBlockHashRPC();

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_GetBlockHash()
        {
            string dir = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.Services.ServiceProvider.GetService<ConsensusController>();

            uint256 result = controller.GetBlockHashRPC(0);

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_IsInitialBlockDownload()
        {
            string dir = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(dir);
            var isIBDProvider = fullNode.NodeService<IInitialBlockDownloadState>(true);

            Assert.NotNull(isIBDProvider);
            Assert.True(isIBDProvider.IsInitialBlockDownload());
        }
    }
}
