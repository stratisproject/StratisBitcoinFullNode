using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class ConsensusActionTests : BaseRPCControllerTest
    {
        private IFullNode fullNode;
        private ConsensusController controller;

        public ConsensusActionTests()
        {
            string dir = "Stratis.Bitcoin.Features.RPC.Tests/TestData/ConsensusActionTests";
            this.fullNode = this.BuildServicedNode(dir);
            this.controller = this.fullNode.Services.ServiceProvider.GetService<ConsensusController>();
        }

        [Fact]
        public void CanCall_GetBestBlockHash()
        {
            uint256 result = this.controller.GetBestBlockHash();

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_GetBlockHash()
        {
            uint256 result = this.controller.GetBlockHash(0);

            Assert.Null(result);
        }

        [Fact]
        public void CanCall_IsInitialBlockDownload()
        {
            var isIBDProvider = this.fullNode.NodeService<IBlockDownloadState>(true);
            Assert.NotNull(isIBDProvider);
            Assert.True(isIBDProvider.IsInitialBlockDownload());       
        }
    }
}
