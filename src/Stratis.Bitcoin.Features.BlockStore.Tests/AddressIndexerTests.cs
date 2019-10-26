using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LiteDB;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;
using FileMode = LiteDB.FileMode;
using Script = NBitcoin.Script;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class AddressIndexerTests
    {
        private readonly IAddressIndexer addressIndexer;

        private readonly Mock<IConsensusManager> consensusManagerMock;

        private readonly Mock<IAsyncProvider> asyncProviderMock;

        private readonly Network network;

        private readonly ChainedHeader genesisHeader;

        public AddressIndexerTests()
        {
            this.network = new StratisMain();
            var storeSettings = new StoreSettings(NodeSettings.Default(this.network));

            storeSettings.AddressIndex = true;
            storeSettings.TxIndex = true;

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            var stats = new Mock<INodeStats>();
            var indexer = new ChainIndexer(this.network);

            this.consensusManagerMock = new Mock<IConsensusManager>();

            this.asyncProviderMock = new Mock<IAsyncProvider>();

            this.addressIndexer = new AddressIndexer(storeSettings, dataFolder, new ExtendedLoggerFactory(), this.network, stats.Object,
                this.consensusManagerMock.Object, this.asyncProviderMock.Object, indexer, new DateTimeProvider());

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

            this.consensusManagerMock.Setup(x => x.GetBlockData(It.IsAny<uint256>())).Returns((uint256 hash) =>
            {
                ChainedHeader header = headers.SingleOrDefault(x => x.HashBlock == hash);

                switch (header?.Height)
                {
                    case 1:
                        return new ChainedHeaderBlock(block1, header);

                    case 5:
                        return new ChainedHeaderBlock(block5, header);

                    case 10:
                        return new ChainedHeaderBlock(block10, header);
                }

                return new ChainedHeaderBlock(new Block(), header);
            });

            this.addressIndexer.Initialize();

            TestBase.WaitLoop(() => this.addressIndexer.IndexerTip == headers.Last());

            Assert.Equal(60_000, this.addressIndexer.GetAddressBalances(new[] { address1 }).Balances.First().Balance.Satoshi);
            Assert.Equal(2_000, this.addressIndexer.GetAddressBalances(new[] { address2 }).Balances.First().Balance.Satoshi);

            Assert.Equal(70_000, this.addressIndexer.GetAddressBalances(new[] { address1 }, 93).Balances.First().Balance.Satoshi);

            // Now trigger rewind to see if indexer can handle reorgs.
            ChainedHeader forkPoint = headers.Single(x => x.Height == 8);

            List<ChainedHeader> headersFork = ChainedHeadersHelper.CreateConsecutiveHeaders(100, forkPoint, false, null, this.network);

            this.consensusManagerMock.Setup(x => x.GetBlockData(It.IsAny<uint256>())).Returns((uint256 hash) =>
            {
                ChainedHeader headerFork = headersFork.SingleOrDefault(x => x.HashBlock == hash);

                return new ChainedHeaderBlock(new Block(), headerFork);
            });

            this.consensusManagerMock.Setup(x => x.Tip).Returns(() => headersFork.Last());
            TestBase.WaitLoop(() => this.addressIndexer.IndexerTip == headersFork.Last());

            Assert.Equal(70_000, this.addressIndexer.GetAddressBalances(new[] { address1 }).Balances.First().Balance.Satoshi);

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

        [Fact]
        public void OutPointCacheCanRetrieveExisting()
        {
            const string CollectionName = "DummyCollection";
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            string dbPath = Path.Combine(dataFolder.RootPath, CollectionName);
            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;

            var database = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });
            var cache = new AddressIndexerOutpointsRepository(database, new ExtendedLoggerFactory());

            var outPoint = new OutPoint(uint256.Parse("0000af9ab2c8660481328d0444cf167dfd31f24ca2dbba8e5e963a2434cffa93"), 0);

            var data = new OutPointData() { Outpoint = outPoint.ToString(), ScriptPubKeyBytes = new byte[] { 0, 0, 0, 0 }, Money = Money.Coins(1) };

            cache.AddOutPointData(data);

            Assert.True(cache.TryGetOutPointData(outPoint, out OutPointData retrieved));

            Assert.NotNull(retrieved);
            Assert.Equal(outPoint.ToString(), retrieved.Outpoint);
        }

        [Fact]
        public void OutPointCacheCannotRetrieveNonexistent()
        {
            const string CollectionName = "DummyCollection";
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            string dbPath = Path.Combine(dataFolder.RootPath, CollectionName);
            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;

            var database = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });
            var cache = new AddressIndexerOutpointsRepository(database, new ExtendedLoggerFactory());

            Assert.False(cache.TryGetOutPointData(new OutPoint(uint256.Parse("0000af9ab2c8660481328d0444cf167dfd31f24ca2dbba8e5e963a2434cffa93"), 1), out OutPointData retrieved));
            Assert.Null(retrieved);
        }

        [Fact]
        public void OutPointCacheEvicts()
        {
            const string CollectionName = "OutputsData";
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            string dbPath = Path.Combine(dataFolder.RootPath, CollectionName);
            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;

            var database = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });
            var cache = new AddressIndexerOutpointsRepository(database, new ExtendedLoggerFactory(), 2);

            Assert.Equal(0, cache.Count);
            Assert.Equal(0, database.GetCollection<OutPointData>(CollectionName).Count());

            var outPoint1 = new OutPoint(uint256.Parse("0000af9ab2c8660481328d0444cf167dfd31f24ca2dbba8e5e963a2434cffa93"), 1); ;
            var pair1 = new OutPointData() { Outpoint = outPoint1.ToString(), ScriptPubKeyBytes = new byte[] { 0, 0, 0, 0 }, Money = Money.Coins(1) };

            cache.AddOutPointData(pair1);

            Assert.Equal(1, cache.Count);
            Assert.Equal(0, database.GetCollection<OutPointData>(CollectionName).Count());

            var outPoint2 = new OutPoint(uint256.Parse("cf8ce1419bbc4870b7d4f1c084534d91126dd3283b51ec379e0a20e27bd23633"), 2); ;
            var pair2 = new OutPointData() { Outpoint = outPoint2.ToString(), ScriptPubKeyBytes = new byte[] { 1, 1, 1, 1 }, Money = Money.Coins(2) };

            cache.AddOutPointData(pair2);

            Assert.Equal(2, cache.Count);
            Assert.Equal(0, database.GetCollection<OutPointData>(CollectionName).Count());

            var outPoint3 = new OutPoint(uint256.Parse("126dd3283b51ec379e0a20e27bd23633cf8ce1419bbc4870b7d4f1c084534d91"), 3); ;
            var pair3 = new OutPointData() { Outpoint = outPoint3.ToString(), ScriptPubKeyBytes = new byte[] { 2, 2, 2, 2 }, Money = Money.Coins(3) };

            cache.AddOutPointData(pair3);

            Assert.Equal(2, cache.Count);

            // One of the cache items should have been evicted, and will therefore be persisted on disk.
            Assert.Equal(1, database.GetCollection<OutPointData>(CollectionName).Count());

            // The evicted item should be pair1.
            Assert.Equal(pair1.ScriptPubKeyBytes, database.GetCollection<OutPointData>(CollectionName).FindAll().First().ScriptPubKeyBytes);

            // It should still be possible to retrieve pair1 from the cache (it will pull it from disk).
            Assert.True(cache.TryGetOutPointData(outPoint1, out OutPointData pair1AfterEviction));

            Assert.NotNull(pair1AfterEviction);
            Assert.Equal(pair1.ScriptPubKeyBytes, pair1AfterEviction.ScriptPubKeyBytes);
            Assert.Equal(pair1.Money, pair1AfterEviction.Money);
        }

        [Fact]
        public void AddressCacheCanRetrieveExisting()
        {
            const string CollectionName = "DummyCollection";
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            string dbPath = Path.Combine(dataFolder.RootPath, CollectionName);
            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;

            var database = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });
            var cache = new AddressIndexRepository(database, new ExtendedLoggerFactory());

            string address = "xyz";
            var balanceChanges = new List<AddressBalanceChange>();

            balanceChanges.Add(new AddressBalanceChange() { BalanceChangedHeight = 1, Deposited = true, Satoshi = 1 });

            var data = new AddressIndexerData() { Address = address, BalanceChanges = balanceChanges };

            cache.AddOrUpdate(data.Address, data, data.BalanceChanges.Count + 1);

            AddressIndexerData retrieved = cache.GetOrCreateAddress("xyz");

            Assert.NotNull(retrieved);
            Assert.Equal("xyz", retrieved.Address);
            Assert.Equal(1, retrieved.BalanceChanges.First().BalanceChangedHeight);
            Assert.True(retrieved.BalanceChanges.First().Deposited);
            Assert.Equal(1, retrieved.BalanceChanges.First().Satoshi);
        }

        [Fact]
        public void AddressCacheRetrievesBlankRecordForNonexistent()
        {
            const string CollectionName = "DummyCollection";
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            string dbPath = Path.Combine(dataFolder.RootPath, CollectionName);
            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;

            var database = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });
            var cache = new AddressIndexRepository(database, new ExtendedLoggerFactory());

            AddressIndexerData retrieved = cache.GetOrCreateAddress("xyz");

            // A record will be returned with no balance changes associated, if it is new.
            Assert.NotNull(retrieved);
            Assert.Equal("xyz", retrieved.Address);
            Assert.Empty(retrieved.BalanceChanges);
        }

        [Fact]
        public void AddressCacheEvicts()
        {
            const string CollectionName = "AddrData";
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            string dbPath = Path.Combine(dataFolder.RootPath, CollectionName);
            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;

            var database = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });
            var cache = new AddressIndexRepository(database, new ExtendedLoggerFactory(), 4);

            // Recall, each index entry counts as 1 and each balance change associated with it is an additional 1.
            Assert.Equal(0, database.GetCollection<AddressIndexerData>(CollectionName).Count());

            string address1 = "xyz";
            var balanceChanges1 = new List<AddressBalanceChange>();
            balanceChanges1.Add(new AddressBalanceChange() { BalanceChangedHeight = 1, Deposited = true, Satoshi = 1 });
            var data1 = new AddressIndexerData() { Address = address1, BalanceChanges = balanceChanges1 };

            cache.AddOrUpdate(data1.Address, data1, data1.BalanceChanges.Count + 1);

            Assert.Equal(0, database.GetCollection<AddressIndexerData>(CollectionName).Count());

            string address2 = "abc";
            var balanceChanges2 = new List<AddressBalanceChange>();
            balanceChanges2.Add(new AddressBalanceChange() { BalanceChangedHeight = 2, Deposited = false, Satoshi = 2 });

            cache.AddOrUpdate(address2, new AddressIndexerData() { Address = address2, BalanceChanges = balanceChanges2 }, balanceChanges2.Count + 1);

            Assert.Equal(0, database.GetCollection<AddressIndexerData>(CollectionName).Count());

            string address3 = "def";
            var balanceChanges3 = new List<AddressBalanceChange>();
            balanceChanges3.Add(new AddressBalanceChange() { BalanceChangedHeight = 3, Deposited = true, Satoshi = 3 });
            cache.AddOrUpdate(address3, new AddressIndexerData() { Address = address3, BalanceChanges = balanceChanges3 }, balanceChanges3.Count + 1);

            // One of the cache items should have been evicted, and will therefore be persisted on disk.
            Assert.Equal(1, database.GetCollection<AddressIndexerData>(CollectionName).Count());

            // The evicted item should be data1.
            Assert.Equal(data1.Address, database.GetCollection<AddressIndexerData>(CollectionName).FindAll().First().Address);
            Assert.Equal(1, database.GetCollection<AddressIndexerData>(CollectionName).FindAll().First().BalanceChanges.First().BalanceChangedHeight);
            Assert.True(database.GetCollection<AddressIndexerData>(CollectionName).FindAll().First().BalanceChanges.First().Deposited);
            Assert.Equal(1, database.GetCollection<AddressIndexerData>(CollectionName).FindAll().First().BalanceChanges.First().Satoshi);

            // Check that the first address can still be retrieved, it should come from disk in this case.
            AddressIndexerData retrieved = cache.GetOrCreateAddress("xyz");

            Assert.NotNull(retrieved);
            Assert.Equal("xyz", retrieved.Address);
            Assert.Equal(1, retrieved.BalanceChanges.First().BalanceChangedHeight);
            Assert.True(retrieved.BalanceChanges.First().Deposited);
            Assert.Equal(1, retrieved.BalanceChanges.First().Satoshi);
        }

        [Fact]
        public void MaxReorgIsCalculatedProperly()
        {
            var btc = new BitcoinMain();

            int maxReorgBtc = AddressIndexer.GetMaxReorgOrFallbackMaxReorg(btc);

            Assert.Equal(maxReorgBtc, AddressIndexer.FallBackMaxReorg);

            var stratis = new StratisMain();

            int maxReorgStratis = AddressIndexer.GetMaxReorgOrFallbackMaxReorg(stratis);

            Assert.Equal(maxReorgStratis, (int)stratis.Consensus.MaxReorgLength);
        }
    }
}
