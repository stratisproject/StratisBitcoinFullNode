using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.Utils;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderRepositoryTests : LogsTestBase
    {
        private readonly Mock<ILoggerFactory> loggerFactory;
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashTable = "BlockHash";

        public ProvenBlockHeaderRepositoryTests() : base(KnownNetworks.StratisTest)
        {
            this.loggerFactory = new Mock<ILoggerFactory>();
        }

        [Fact]
        public async Task InitializesGenesisProvenBlockHeaderOnFirstLoadAsync()
        {
            string folder = CreateTestDir(this);

            // Initialise the repository - this will set-up the genesis blockHash (blockId).
            using (IProvenBlockHeaderRepository repository = this.SetupRepository(this.Network, folder))
            {
                // Check the BlockHash (blockId) exists.
                HashHeightPair TipHashtask = await repository.GetTipHashHeightAsync();

                TipHashtask.Height.Should().Be(0);
            }
        }

        [Fact]
        public async Task PutAsync_WritesProvenBlockHeaderAndSavesBlockHashAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader provenBlockHeaderIn = CreateNewProvenBlockHeaderMock();

            var blockHashHieghtPair = new HashHeightPair(new uint256(), 1);
            var items = new List<ProvenBlockHeader> { provenBlockHeaderIn };

            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                await repo.PutAsync(items, blockHashHieghtPair);
            }

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.SynchronizeTables(ProvenBlockHeaderTable);
                txn.ValuesLazyLoadingIsOn = false;

                var headerOut = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHashHieghtPair.Height.ToBytes(false)).Value;

                headerOut.Should().NotBeNull();
                headerOut.GetHash().Should().Be(provenBlockHeaderIn.GetHash());
            }
        }

        [Fact]
        public async Task PutAsync_WritesMultipleProvenBlockHeadersAsync()
        {
            string folder = CreateTestDir(this);

            PosBlock posBlock = CreatePosBlockMock();
            ProvenBlockHeader header1 = CreateNewProvenBlockHeaderMock(posBlock);
            ProvenBlockHeader header2 = CreateNewProvenBlockHeaderMock(posBlock);

            var items = new List<ProvenBlockHeader> { header1, header2 };

            // Put the items in the repository.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                await repo.PutAsync(items, new HashHeightPair(new uint256(), items.Count));
            }

            // Check the ProvenBlockHeader exists in the database - and are in sorted order.
            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.SynchronizeTables(ProvenBlockHeaderTable);
                txn.ValuesLazyLoadingIsOn = false;

                var headersOut = txn.SelectDictionary<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable);

                headersOut.Keys.Count.Should().Be(2);
                headersOut.First().Value.GetHash().Should().Be(items[0].GetHash());
                headersOut.Last().Value.GetHash().Should().Be(items[1].GetHash());
            }
        }

        [Fact]
        public async Task GetAsync_ReadsProvenBlockHeaderFromDatabaseAndDoesNotOverwriteOnFirstLoadAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader headerIn = CreateNewProvenBlockHeaderMock();

            int blockHeight = 1;

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeight.ToBytes(false), headerIn);
                txn.Commit();
            }

            // Query the repository for the item that was inserted in the above code.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                var headerOut = await repo.GetAsync(blockHeight).ConfigureAwait(false);

                headerOut.Should().NotBeNull();
                uint256.Parse(headerOut.ToString()).Should().Be(headerOut.GetHash());
            }
        }

        [Fact]
        public async Task GetAsync_ReadsMultipleProvenBlockHeaderFromDatabaseAndDoesNotOverwriteOnFirstLoadAsync()
        {
            string folder = CreateTestDir(this);

            PosBlock posBlock = CreatePosBlockMock();
            ProvenBlockHeader header1 = CreateNewProvenBlockHeaderMock(posBlock);
            ProvenBlockHeader header2 = CreateNewProvenBlockHeaderMock(posBlock);

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, 1.ToBytes(false), header1);
                txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, 2.ToBytes(false), header2);
                txn.Commit();
            }

            // Query the repository for the item that was inserted in the above code.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                List<ProvenBlockHeader> headersOut = await repo.GetAsync(1, 2).ConfigureAwait(false);

                headersOut.Count.Should().Be(2);
                headersOut.First().GetHash().Should().Be(header1.GetHash());
                headersOut.Last().GetHash().Should().Be(header2.GetHash());
            }
        }

        [Fact]
        public async Task GetAsync_WithWrongBlockHeightReturnsNullAsync()
        {
            string folder = CreateTestDir(this);

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, 1.ToBytes(false), CreateNewProvenBlockHeaderMock());
                txn.Insert<byte[], HashHeightPair>(BlockHashTable, new byte[0], new HashHeightPair(new uint256(), 1));
                txn.Commit();
            }
            
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                // Select a different block height.
                ProvenBlockHeader outHeader = await repo.GetAsync(2).ConfigureAwait(false);
                outHeader.Should().BeNull();

                // Select the original item inserted into the table
                outHeader = await repo.GetAsync(1).ConfigureAwait(false);
                outHeader.Should().NotBeNull();
            }
        }

        [Fact(Skip = "Used only during rewinding (and only for not overridden headers)")]
        public async Task DeleteAsync_RemovesProvenBlockHeadersAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader headerIn = CreateNewProvenBlockHeaderMock();

            var items = new List<ProvenBlockHeader> { headerIn, headerIn, };

            int[] blockHeights = { 1, 2 };
            var newTip = new HashHeightPair(new uint256(), blockHeights.Count());

            // Add items and verify they exist.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                await repo.PutAsync(items, newTip).ConfigureAwait(false);
            }

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();

                txn.SynchronizeTables(ProvenBlockHeaderTable);

                txn.ValuesLazyLoadingIsOn = false;

                var row = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeights[0].ToBytes(false));
                row.Exists.Should().BeTrue();

                row = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeights[1].ToBytes(false));
                row.Exists.Should().BeTrue();
            }

            // Delete the items and verify they no longer exist.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                // TODO
                // await repo.DeleteAsync(3, blockHeights.ToList());
            }

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();

                txn.SynchronizeTables(ProvenBlockHeaderTable);

                txn.ValuesLazyLoadingIsOn = false;

                var row = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeights[0].ToBytes(false));
                row.Exists.Should().BeFalse();

                row = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockHeights[1].ToBytes(false));
                row.Exists.Should().BeFalse();
            }
        }

        private IProvenBlockHeaderRepository SetupRepository(Network network, string folder)
        {
            var repo = new ProvenBlockHeaderRepository(network, folder, this.LoggerFactory.Object);

            Task task = repo.InitializeAsync();
            task.Wait();

            return repo;
        }
    }
}
