using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class ConsensusActionTests : BaseRPCControllerTest
    {
        public ConsensusActionTests()
        {
        }

        [Fact]
        public void CanCall_GetBestBlockHash()
        {
            string dir = AssureEmptyDir("TestData/ConsensusActionTests/CanCall_GetBestBlockHash");

            var fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.Services.ServiceProvider.GetService<ConsensusController>();

            uint256 result = controller.GetBestBlockHash();

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_GetBlockHash()
        {
            string dir = AssureEmptyDir("TestData/ConsensusActionTests/CanCall_GetBlockHash");

            var fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.Services.ServiceProvider.GetService<ConsensusController>();

            uint256 result = controller.GetBlockHash(0);

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_IsInitialBlockDownload()
        {
            string dir = AssureEmptyDir("TestData/ConsensusActionTests/CanCall_IsInitialBlockDownload");

            var fullNode = this.BuildServicedNode(dir);
            var isIBDProvider = fullNode.NodeService<IBlockDownloadState>(true);

            Assert.NotNull(isIBDProvider);
            Assert.True(isIBDProvider.IsInitialBlockDownload());       
        }
    }
}
