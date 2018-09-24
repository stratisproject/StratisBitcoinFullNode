using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreTests : LogsTestBase
    {
        private readonly Network network = KnownNetworks.StratisTest;
        private readonly Mock<IConsensusManager> consensusManager;
        private readonly Mock<ILoggerFactory> loggerFactory;

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.loggerFactory = new Mock<ILoggerFactory>();
        }

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
            //var store = new ProvenBlockHeaderStore(network, DateTimeProvider.Default, this.LoggerFactory.Object, this.nodeStats);

            //store.LoadAsync().GetAwaiter().GetResult();

            //return store;

            return null;
        }
    }
}
