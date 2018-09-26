using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
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

        private readonly NodeStats nodeStats;


        public ProvenBlockHeaderRepositoryTests() : base(KnownNetworks.StratisTest)
        {
            this.nodeStats = new NodeStats(DateTimeProvider.Default);
            this.loggerFactory = new Mock<ILoggerFactory>();
        }

        [Fact]
        public async Task InitializesGenesisProvenBlockHeaderOnFirstLoadAsync()
        {
            string folder = CreateTestDir(this);
            uint256 blockId;

            // Initialise the repository - this will set-up the genesis blockHash (blockId).
            using (IProvenBlockHeaderRepository repository = this.SetupRepository(this.Network, folder))
            {
                // Check the BlockHash (blockId) exists.
                uint256 TipHashtask = await repository.GetTipHashAsync();

                blockId = TipHashtask;
                blockId.Should().Be(this.Network.GetGenesis().GetHash());
            }
        }

        [Fact]
        public async Task PutAsyncWritesProvenBlockHeaderAndSavesBlockHashAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader provenBlockHeaderIn = CreateNewProvenBlockHeaderMock();
            uint256 blockInHash = provenBlockHeaderIn.GetHash();

            // Add the ProvenBlockHeader block hash to the database.
            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.Insert<byte[], uint256>(BlockHashTable, new byte[0], blockInHash);
                txn.Commit();
            }

            // Add a couple of ProvenBlockHeaders into the database via PutAsync().
            var items = new List<StakeItem>
            {
                new StakeItem
                {
                    BlockId = blockInHash,
                    Height = 1,
                    ProvenBlockHeader = provenBlockHeaderIn
                },
                new StakeItem
                {
                    BlockId = uint256.One,
                    Height = 2,
                    ProvenBlockHeader = provenBlockHeaderIn
                },
            };

            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                await repo.PutAsync(items);
            }

            // Check the above items exits in the database.
            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.SynchronizeTables(ProvenBlockHeaderTable);
                txn.ValuesLazyLoadingIsOn = false;

                var blockOut = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockInHash.ToBytes(false)).Value;

                blockOut.Should().NotBeNull();
                blockOut.GetHash().Should().Be(blockInHash);
            }
        }

        [Fact]
        public async Task PutAsyncWritesProvenBlockHeadersInSortedOrderAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader provenBlockHeaderIn = CreateNewProvenBlockHeaderMock();

            // Build up list of items not sorted - BlockId's are the wrong way around.
            var items = new List<StakeItem>
            {
                new StakeItem
                {
                    BlockId = uint256.One,
                    ProvenBlockHeader = provenBlockHeaderIn
                },
                new StakeItem
                {
                    BlockId = uint256.Zero,
                    ProvenBlockHeader = provenBlockHeaderIn
                },
            };

            // Put the items in the repository.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                await repo.PutAsync(items);
            }

            // Check the ProvenBlockHeader exists in the database - and are in sorted order.
            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.SynchronizeTables(ProvenBlockHeaderTable);
                txn.ValuesLazyLoadingIsOn = false;

                var provenBlockHeaderAll = txn.SelectDictionary<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable);

                provenBlockHeaderAll.Keys.Count.Should().Be(2);

                // Check items are sorted - item[1] was added last in the above code, check that item is now first.
                provenBlockHeaderAll.FirstOrDefault().Key.Equals(items[1].BlockId.ToBytes());
                provenBlockHeaderAll.LastOrDefault().Key.Equals(items[0].BlockId.ToBytes());
            }
        }

        [Fact]
        public async Task GetAsyncReadsProvenBlockHeaderFromDatabaseAndDoesNotOverwriteOnFirstLoadAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader provenBlockHeader = CreateNewProvenBlockHeaderMock();
            uint256 hash = provenBlockHeader.GetHash();

            // Set-up item and insert into the database.
            var item = new List<StakeItem>
            {
                new StakeItem
                {
                    BlockId = hash,
                    Height = 1,
                    ProvenBlockHeader = provenBlockHeader
                }
            };

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.Insert<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, item[0].BlockId.ToBytes(false), item[0].ProvenBlockHeader);
                txn.Insert<byte[], uint256>(BlockHashTable, new byte[0], item[0].BlockId);
                txn.Commit();
            }

            // Query the repository for the item that was inserted in the above code.
            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                await repo.GetAsync(item).ConfigureAwait(false);
            }

            item[0].ProvenBlockHeader.Should().NotBeNull();
            item[0].InStore.Should().BeTrue();
            uint256.Parse(item[0].ProvenBlockHeader.ToString()).Should().Be(hash);
        }

        private IProvenBlockHeaderRepository SetupRepository(Network network, string folder)
        {
            var repo = new ProvenBlockHeaderRepository(network, folder, DateTimeProvider.Default, this.LoggerFactory.Object, this.nodeStats);

            Task task = repo.InitializeAsync();
            task.Wait();

            return repo;
        }
    }
}
