using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
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
        public void InitializesGenesisProvenBlockHeaderOnFirstLoad()
        {
            string folder = CreateTestDir(this);

            using (IProvenBlockHeaderRepository repository = this.SetupRepository(this.Network, folder))
            {
            }

            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                Row<byte[], uint256> row = transaction.Select<byte[], uint256>(BlockHashTable, new byte[0]);

                row.Value.Should().Be(this.Network.GetGenesis().GetHash());
            }
        }

        [Fact]
        public void PutAsyncWritesProvenBlockHeaderAndSavesBlockHash()
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

            var item = new List<StakeItem>
            {
                new StakeItem
                {
                    BlockId = blockInHash,
                    Height = 1,
                    ProvenBlockHeader = provenBlockHeaderIn
                }
            };

            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                Task task = repo.PutAsync(item);
                task.Wait();
            }

            // Add ProvenBlockHeader to the database.
            using (var engine = new DBreezeEngine(folder))
            {
                DBreeze.Transactions.Transaction txn = engine.GetTransaction();
                txn.SynchronizeTables(ProvenBlockHeaderTable);
                txn.ValuesLazyLoadingIsOn = false;

                var blockOut = txn.Select<byte[], ProvenBlockHeader>(ProvenBlockHeaderTable, blockInHash.ToBytes()).Value;

                blockOut.Should().NotBeNull();
                blockOut.GetHash().Should().Be(blockInHash);
            }
        }

        [Fact]
        public void GetAsyncReadsProvenBlockHeaderFromTheDatabase()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader provenBlockHeader = CreateNewProvenBlockHeaderMock();
            uint256 hash = provenBlockHeader.GetHash();

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
                txn.Commit();
            }

            using (IProvenBlockHeaderRepository repo = this.SetupRepository(this.Network, folder))
            {
                Task task = repo.GetAsync(item);
                task.Wait();
            }

            item[0].ProvenBlockHeader.Should().NotBeNull();
            item[0].InStore.Should().BeTrue();
            uint256.Parse(item[0].ProvenBlockHeader.ToString()).Should().Be(hash);
        }

        private IProvenBlockHeaderRepository SetupRepository(Network network, string folder)
        {
            var repo = new ProvenBlockHeaderRepository(network, folder, DateTimeProvider.Default, this.LoggerFactory.Object, nodeStats);

            repo.InitializeAsync().GetAwaiter().GetResult();

            return repo;
        }
    }
}
