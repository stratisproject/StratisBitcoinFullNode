using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class AddressIndexerTests
    {
        private readonly AddressIndexer addressIndexer;

        private readonly Mock<IBlockStore> blockStoreMock;

        private readonly Mock<IConsensusManager> consensusManagerMock;

        private readonly Network network;

        private readonly ChainedHeader genesisHeader;

        public AddressIndexerTests()
        {
            this.network = new StratisMain();
            var storeSettings = new StoreSettings(NodeSettings.Default(this.network));

            storeSettings.AddressIndex = true;
            storeSettings.TxIndex = true;

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            this.blockStoreMock = new Mock<IBlockStore>();
            var stats = new Mock<INodeStats>();
            this.consensusManagerMock = new Mock<IConsensusManager>();

            this.addressIndexer = new AddressIndexer(storeSettings, dataFolder, new ExtendedLoggerFactory(), this.network, this.blockStoreMock.Object,
                stats.Object, this.consensusManagerMock.Object);

            this.genesisHeader = new ChainedHeader(this.network.GetGenesis().Header, this.network.GetGenesis().Header.GetHash(), 0);
        }

        [Fact]
        public void CanInitializeAndDispose()
        {
            this.consensusManagerMock.Setup(x => x.Tip).Returns(() => this.genesisHeader);

            this.addressIndexer.Initialize();
            this.addressIndexer.Dispose();
        }

        [Fact]
        public void CanIndexAddresses()
        {
            // TODO implement test
        }

        [Fact]
        public void CanHandleForks()
        {
            // TODO implement test
        }
    }
}
