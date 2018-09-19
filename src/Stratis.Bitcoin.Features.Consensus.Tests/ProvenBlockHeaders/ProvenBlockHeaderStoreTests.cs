using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreTests : LogsTestBase
    {
        [Fact]
        public void InitializesProvenBlockHeaderOnFirstLoad()
        {
            string folder = CreateTestDir(this);

            using (IProvenBlockHeaderStore store = this.SetupStore(this.Network, folder))
            {
            }
        }

        private IProvenBlockHeaderStore SetupStore(Network network, string folder)
        {
            var nodeStats = new NodeStats(DateTimeProvider.Default);

            var store = new ProvenBlockHeaderStore(network, folder, DateTimeProvider.Default, this.LoggerFactory.Object, nodeStats);

            store.InitializeAsync().GetAwaiter().GetResult();

            return store;
        }
    }
}
