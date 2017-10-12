namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    using DBreeze;
    using NBitcoin;
    using Stratis.Bitcoin.Tests;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class BlockRepositoryTest : TestBase
    {
        [Fact]
        public async Task InitializesGenBlockAndTxIndexOnFirstLoadAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/InitializeGenBlockAndTxIndex");
            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                var blockRow = transaction.Select<byte[], uint256>("Common", new byte[0]);
                var txIndexRow = transaction.Select<byte[], bool>("Common", new byte[1]);

                Assert.Equal(Network.Main.GetGenesis().GetHash(), blockRow.Value);
                Assert.False(txIndexRow.Value);
            }
        }

        [Fact]
        public async Task DoesNotOverwriteExistingBlockAndTxIndexOnFirstLoadAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/NoOverwriteExistingBlockAndTxIndex");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                transaction.Insert<byte[], byte[]>("Common", new byte[0], new uint256(56).ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                var blockRow = transaction.Select<byte[], uint256>("Common", new byte[0]);
                var txIndexRow = transaction.Select<byte[], bool>("Common", new byte[1]);

                Assert.Equal(new uint256(56), blockRow.Value);
                Assert.True(txIndexRow.Value);
            }
        }

        [Fact]
        public async Task GetTrxAsyncWithoutTransactionIndexReturnsNewTransactionAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxAsyncWithoutTxIndex");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetTrxAsync(uint256.Zero).ConfigureAwait(false);

                Assert.Equal(default(Transaction), result);
            }
        }

        [Fact]
        public async Task GetTrxAsyncWithoutTransactionInIndexReturnsNullAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxAsyncWithoutTransactionFound");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                var blockId = new uint256(8920);
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetTrxAsync(new uint256(65)).ConfigureAwait(false);

                Assert.Null(result);
            }
        }

        [Fact]
        public async Task GetTrxAsyncWithTransactionReturnsExistingTransactionAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxAsyncWithTransaction");
            var trans = new Transaction();
            trans.Version = 125;

            using (var engine = new DBreezeEngine(dir))
            {
                var block = new Block();
                block.Header.GetHash();
                block.Transactions.Add(trans);

                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.Header.GetHash().ToBytes(), block.ToBytes());
                transaction.Insert<byte[], byte[]>("Transaction", trans.GetHash().ToBytes(), block.Header.GetHash().ToBytes());
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetTrxAsync(trans.GetHash()).ConfigureAwait(false);

                Assert.Equal((uint)125, result.Version);
            }
        }

        [Fact]
        public async Task GetTrxBlockIdAsyncWithoutTxIndexReturnsDefaultIdAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxBlockIdAsyncWithoutTxIndex");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetTrxBlockIdAsync(new uint256(26)).ConfigureAwait(false);

                Assert.Equal(default(uint256), result);
            }
        }

        [Fact]
        public async Task GetTrxBlockIdAsyncWithoutExistingTransactionReturnsNullAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxBlockIdAsyncWithoutTransaction");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetTrxBlockIdAsync(new uint256(26)).ConfigureAwait(false);

                Assert.Null(result);
            }
        }

        [Fact]
        public async Task GetTrxBlockIdAsyncWithTransactionReturnsBlockIdAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxBlockIdAsyncWithoutTransaction");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Transaction", new uint256(26).ToBytes(), new uint256(42).ToBytes());
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetTrxBlockIdAsync(new uint256(26)).ConfigureAwait(false);
                
                Assert.Equal(new uint256(42), result);
            }
        }

        [Fact]
        public async Task PutAsyncWritesBlocksAndTransactionsToDbAndSavesNextBlockHashAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/PutAsyncStoresBlocksAndTxs");

            var nextBlockHash = new uint256(1241256);
            var blocks = new List<Block>();
            var blockHeader = new BlockHeader();
            blockHeader.Bits = new Target(12);
            var block = new Block(blockHeader);
            var transaction = new Transaction();
            transaction.Version = 32;
            block.Transactions.Add(transaction);
            transaction = new Transaction();
            transaction.Version = 48;
            block.Transactions.Add(transaction);
            blocks.Add(block);

            var blockHeader2 = new BlockHeader();
            var block2 = new Block(blockHeader2);
            transaction = new Transaction();
            transaction.Version = 15;
            block2.Transactions.Add(transaction);
            blocks.Add(block2);

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();
                trans.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                trans.Insert<byte[], bool>("Common", new byte[1], true);
                trans.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                await repository.PutAsync(nextBlockHash, blocks).ConfigureAwait(false);
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var blockHashKeyRow = trans.Select<byte[], uint256>("Common", new byte[0]);
                var blockDict = trans.SelectDictionary<byte[], byte[]>("Block");
                var transDict = trans.SelectDictionary<byte[], byte[]>("Transaction");

                Assert.Equal(nextBlockHash, blockHashKeyRow.Value);
                Assert.Equal(2, blockDict.Count);
                Assert.Equal(3, transDict.Count);

                foreach (var item in blockDict)
                {
                    var bl = blocks.Where(b => b.GetHash() == new uint256(item.Key)).Single();
                    Assert.Equal(bl.Header.GetHash(), new Block(item.Value).Header.GetHash());
                }

                foreach (var item in transDict)
                {
                    var bl = blocks.Where(b => b.Transactions.Any(t => t.GetHash() == new uint256(item.Key))).Single();
                    Assert.Equal(bl.GetHash(), new uint256(item.Value));
                }
            }
        }

        [Fact]
        public async Task SetTxIndexUpdatesTxIndexAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/SetTxIndexUpdatesTxIndex");
            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();
                trans.Insert<byte[], bool>("Common", new byte[1], true);
                trans.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                await repository.SetTxIndex(false).ConfigureAwait(false);
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var txIndexRow = trans.Select<byte[], bool>("Common", new byte[1]);
                Assert.False(txIndexRow.Value);
            }
        }

        [Fact]
        public async Task SetBlockHashUpdatesBlockHashAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/SetBlockHashUpdatesBlockHash");
            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();
                trans.Insert<byte[], byte[]>("Common", new byte[0], new uint256(41).ToBytes());
                trans.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                await repository.SetBlockHash(new uint256(56)).ConfigureAwait(false);
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var blockHashKeyRow = trans.Select<byte[], byte[]>("Common", new byte[0]);
                Assert.Equal(new uint256(56), new uint256(blockHashKeyRow.Value));
            }
        }

        [Fact]
        public async Task GetAsyncWithExistingBlockReturnsBlockAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetAsyncWithExistingBlock");
            var block = new Block();

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetAsync(block.GetHash()).ConfigureAwait(false);

                Assert.Equal(block.GetHash(), result.GetHash());
            }
        }

        [Fact]
        public async Task GetAsyncWithoutExistingBlockReturnsNullAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetAsyncWithoutExistingBlock");

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetAsync(new uint256()).ConfigureAwait(false);

                Assert.Null(result);
            }
        }

        [Fact]
        public async Task ExistAsyncWithExistingBlockReturnsTrueAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/ExistAsyncWithExistingBlock");
            var block = new Block();

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.ExistAsync(block.GetHash()).ConfigureAwait(false);

                Assert.True(result);
            }
        }

        [Fact]
        public async Task ExistAsyncWithoutExistingBlockReturnsFalseAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/ExistAsyncWithoutExistingBlock");

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.ExistAsync(new uint256()).ConfigureAwait(false);
                
                Assert.False(result);
            }
        }

        [Fact]
        public async Task DeleteAsyncRemovesBlocksAndTransactionsAsync()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/DeleteAsyncRemovesBlocksAndTransactions");
            var block = new Block();
            block.Transactions.Add(new Transaction());

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Insert<byte[], byte[]>("Transaction", block.Transactions[0].GetHash().ToBytes(), block.GetHash().ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                await repository.DeleteAsync(new uint256(45), new List<uint256>() { block.GetHash() }).ConfigureAwait(false);
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var blockHashKeyRow = trans.Select<byte[], uint256>("Common", new byte[0]);
                var blockDict = trans.SelectDictionary<byte[], byte[]>("Block");
                var transDict = trans.SelectDictionary<byte[], byte[]>("Transaction");

                Assert.Equal(new uint256(45), blockHashKeyRow.Value);
                Assert.Equal(0, blockDict.Count);
                Assert.Equal(0, transDict.Count);
            }
        }

        private async Task<BlockStore.IBlockRepository> SetupRepositoryAsync(Network main, string dir)
        {
            var repository = new BlockRepository(main, dir, this.loggerFactory);
            await repository.Initialize().ConfigureAwait(false);

            return repository;
        }
    }
}