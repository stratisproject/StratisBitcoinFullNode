using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class ConsensusActionTests : BaseRPCControllerTest
    {
        public ConsensusActionTests()
        {
        }

        [Fact]
        public void CanCall_GetBestBlockHash()
        {
            string dir = CreateTestDir(this);

            var fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.Services.ServiceProvider.GetService<ConsensusController>();

            uint256 result = controller.GetBestBlockHash();

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_GetBlockHash()
        {
            string dir = CreateTestDir(this);

            var fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.Services.ServiceProvider.GetService<ConsensusController>();

            uint256 result = controller.GetBlockHash(0);

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_IsInitialBlockDownload()
        {
            string dir = CreateTestDir(this);

            var fullNode = this.BuildServicedNode(dir);
            var isIBDProvider = fullNode.NodeService<IInitialBlockDownloadState>(true);

            Assert.NotNull(isIBDProvider);
            Assert.True(isIBDProvider.IsInitialBlockDownload());
        }
    }
}
