using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests
{
    /// <summary>
    /// This network enables a mutable Consensus object for testing only.
    /// </summary>
    internal class TestRegTest : BitcoinRegTest
    {
        public TestRegTest()
        {
            this.Name = "TestRegTest";
            this.Consensus = new MutableTestConsensus(this.Consensus);
        }
    }
}