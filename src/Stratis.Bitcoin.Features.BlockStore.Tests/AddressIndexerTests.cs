using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class AddressIndexerTests
    {
        private readonly IAddressIndexer addressIndexer;

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
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100, null, false, null, this.network);
            this.consensusManagerMock.Setup(x => x.Tip).Returns(() => headers.Last());

            Script p2pk1 = this.GetRandomP2PKScript(out string address1);
            Script p2pk2 = this.GetRandomP2PKScript(out string address2);

            var block1 = new Block()
            {
                Transactions = new List<Transaction>()
                {
                    new Transaction()
                    {
                        Outputs =
                        {
                            new TxOut(new Money(10_000), p2pk1),
                            new TxOut(new Money(20_000), p2pk1),
                            new TxOut(new Money(30_000), p2pk1)
                        }
                    }
                }
            };

            var block5 = new Block()
            {
                Transactions = new List<Transaction>()
                {
                    new Transaction()
                    {
                        Outputs =
                        {
                            new TxOut(new Money(10_000), p2pk1),
                            new TxOut(new Money(1_000), p2pk2),
                            new TxOut(new Money(1_000), p2pk2)
                        }
                    }
                }
            };

            var tx = new Transaction();
            tx.Inputs.Add(new TxIn(new OutPoint(block5.Transactions.First().GetHash(), 0)));
            var block10 = new Block() { Transactions = new List<Transaction>() { tx } };

            this.blockStoreMock.Setup(x => x.GetTransactionsByIds(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>())).Returns((uint256[] hashes, CancellationToken token) =>
            {
                if (hashes.Length == 1 && hashes[0] == block5.Transactions.First().GetHash())
                    return new Transaction[] { block5.Transactions.First() };

                return null;
            });

            this.consensusManagerMock.Setup(x => x.GetBlockData(It.IsAny<uint256>())).Returns((uint256 hash) =>
            {
                ChainedHeader header = headers.SingleOrDefault(x => x.HashBlock == hash);

                switch (header?.Height)
                {
                    case 1:
                        return new ChainedHeaderBlock(block1, header);

                    case 5:
                        return new ChainedHeaderBlock(block5, header); ;

                    case 10:
                        return new ChainedHeaderBlock(block10, header); ;
                }

                return  new ChainedHeaderBlock(new Block(), header);;
            });

            this.addressIndexer.Initialize();

            TestHelper.WaitLoop(() => this.addressIndexer.IndexerTip == headers.Last());

            Dictionary<string, List<AddressBalanceChange>> index = this.addressIndexer.GetAddressIndexCopy();
            Assert.Equal(2, index.Keys.Count);

            Assert.Equal(60_000, this.addressIndexer.GetAddressBalance(address1).Satoshi);
            Assert.Equal(2_000, this.addressIndexer.GetAddressBalance(address2).Satoshi);

            Assert.Equal(70_000, this.addressIndexer.GetAddressBalance(address1, 93).Satoshi);

            // Now trigger rewind to see if indexer can handle reorgs.
            ChainedHeader forkPoint = headers.Single(x => x.Height == 8);

            List<ChainedHeader> headersFork = ChainedHeadersHelper.CreateConsecutiveHeaders(100, forkPoint, false, null, this.network);

            this.consensusManagerMock.Setup(x => x.GetBlockData(It.IsAny<uint256>())).Returns((uint256 hash) =>
            {
                ChainedHeader header = headersFork.SingleOrDefault(x => x.HashBlock == hash);
                return new ChainedHeaderBlock(new Block(), header); ;
            });

            this.consensusManagerMock.Setup(x => x.Tip).Returns(() => headersFork.Last());
            TestHelper.WaitLoop(() => this.addressIndexer.IndexerTip == headersFork.Last());

            Assert.Equal(70_000, this.addressIndexer.GetAddressBalance(address1).Satoshi);

            this.addressIndexer.Dispose();
        }

        private Script GetRandomP2PKScript(out string address)
        {
            var bytes = RandomUtils.GetBytes(33);
            bytes[0] = 0x02;

            Script script = new Script() + Op.GetPushOp(bytes) + OpcodeType.OP_CHECKSIG;

            PubKey[] destinationKeys = script.GetDestinationPublicKeys(this.network);
            address = destinationKeys[0].GetAddress(this.network).ToString();

            return script;
        }
    }
}
