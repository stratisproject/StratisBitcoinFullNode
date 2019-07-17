using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.IntegrationTests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class MempoolActionTests : BaseRPCControllerTest
    {
        [Fact]
        public async Task CanCall_GetRawMempoolAsync()
        {
            string dir = CreateTestDir(this);
            IFullNode fullNode = this.BuildServicedNode(dir);
            var controller = fullNode.NodeController<MempoolController>();

            List<uint256> result = await controller.GetRawMempool();

            Assert.NotNull(result);
        }
    }
}
