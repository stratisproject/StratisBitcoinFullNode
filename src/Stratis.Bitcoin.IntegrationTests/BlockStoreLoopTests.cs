using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public sealed class BlockStoreLoopTests
    {
        /// <summary>
        /// Test a full run of the BlockStoreLoop, ensuring that each step executes
        /// and gives the desired result.
        /// <para>
        /// This integration test injects the BlockStoreLoop with concrete implementation of
        /// BlockRepository.
        /// </para>
        /// </summary>
        [Fact]
        public void BlockStoreLoopIntegration()
        {

        }
    }
}