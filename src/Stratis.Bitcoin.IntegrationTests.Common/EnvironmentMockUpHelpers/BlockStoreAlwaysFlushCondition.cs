using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class BlockStoreAlwaysFlushCondition : IBlockStoreQueueFlushCondition
    {
        public bool ShouldFlush => true;
    }
}
