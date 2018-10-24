using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools
{
    public static class CoreNodePoAExtensions
    {
        public static void EnableFastMining(this CoreNode node)
        {
            (node.FullNode.NodeService<IPoAMiner>() as TestPoAMiner).EnableFastMining();
        }
    }
}
