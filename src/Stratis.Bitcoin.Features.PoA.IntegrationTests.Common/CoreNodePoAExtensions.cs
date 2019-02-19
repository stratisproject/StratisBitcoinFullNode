using System.Threading.Tasks;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Common
{
    public static class CoreNodePoAExtensions
    {
        public static async Task MineBlocksAsync(this CoreNode node, int count)
        {
            await (node.FullNode.NodeService<IPoAMiner>() as TestPoAMiner).MineBlocksAsync(count);
        }
    }
}
