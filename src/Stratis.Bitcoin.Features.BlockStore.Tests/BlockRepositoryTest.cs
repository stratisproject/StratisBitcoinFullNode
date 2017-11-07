using System.Collections.Generic;
using System.Linq;
using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Tests;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockRepositoryTest : TestBase
    {
        [Fact]
        public void InitializesGenBlockAndTxIndexOnFirstLoad()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/InitializeGenBlockAndTxIndex");
            using (var repository = SetupRepository(Network.Main, dir))
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
        public void DoesNotOverwriteExistingBlockAndTxIndexOnFirstLoad()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/NoOverwriteExistingBlockAndTxIndex");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                transaction.Insert<byte[], byte[]>("Common", new byte[0], new uint256(56).ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
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
        public void GetTrxAsyncWithoutTransactionIndexReturnsNewTransaction()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxAsyncWithoutTxIndex");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetTrxAsync(uint256.Zero);
                task.Wait();

                Assert.Equal(default(Transaction), task.Result);
            }
        }

        [Fact]
        public void GetTrxAsyncWithoutTransactionInIndexReturnsNull()
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

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetTrxAsync(new uint256(65));
                task.Wait();

                Assert.Null(task.Result);
            }
        }

        [Fact]
        public void GetTrxAsyncWithTransactionReturnsExistingTransaction()
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

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetTrxAsync(trans.GetHash());
                task.Wait();

                Assert.Equal((uint)125, task.Result.Version);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithoutTxIndexReturnsDefaultId()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxBlockIdAsyncWithoutTxIndex");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetTrxBlockIdAsync(new uint256(26));
                task.Wait();

                Assert.Equal(default(uint256), task.Result);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithoutExistingTransactionReturnsNull()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetTrxBlockIdAsyncWithoutTransaction");

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetTrxBlockIdAsync(new uint256(26));
                task.Wait();

                Assert.Null(task.Result);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithTransactionReturnsBlockId()
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

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetTrxBlockIdAsync(new uint256(26));
                task.Wait();

                Assert.Equal(new uint256(42), task.Result);
            }
        }

        [Fact]
        public void PutAsyncWritesBlocksAndTransactionsToDbAndSavesNextBlockHash()
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

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.PutAsync(nextBlockHash, blocks);
                task.Wait();
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
                    var bl = blocks.Single(b => b.GetHash() == new uint256(item.Key));
                    Assert.Equal(bl.Header.GetHash(), new Block(item.Value).Header.GetHash());
                }

                foreach (var item in transDict)
                {
                    var bl = blocks.Single(b => b.Transactions.Any(t => t.GetHash() == new uint256(item.Key)));
                    Assert.Equal(bl.GetHash(), new uint256(item.Value));
                }
            }
        }

        [Fact]
        public void SetTxIndexUpdatesTxIndex()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/SetTxIndexUpdatesTxIndex");
            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();
                trans.Insert<byte[], bool>("Common", new byte[1], true);
                trans.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.SetTxIndexAsync(false);
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var txIndexRow = trans.Select<byte[], bool>("Common", new byte[1]);
                Assert.False(txIndexRow.Value);
            }
        }

        [Fact]
        public void SetBlockHashUpdatesBlockHash()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/SetBlockHashUpdatesBlockHash");
            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();
                trans.Insert<byte[], byte[]>("Common", new byte[0], new uint256(41).ToBytes());
                trans.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.SetBlockHashAsync(new uint256(56));
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var blockHashKeyRow = trans.Select<byte[], byte[]>("Common", new byte[0]);
                Assert.Equal(new uint256(56), new uint256(blockHashKeyRow.Value));
            }
        }

        [Fact]
        public void GetAsyncWithExistingBlockReturnsBlock()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetAsyncWithExistingBlock");
            var block = new Block();

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetAsync(block.GetHash());
                task.Wait();

                Assert.Equal(block.GetHash(), task.Result.GetHash());
            }
        }

        [Fact]
        public void GetAsyncWithoutExistingBlockReturnsNull()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/GetAsyncWithoutExistingBlock");

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.GetAsync(new uint256());
                task.Wait();

                Assert.Null(task.Result);
            }
        }

        [Fact]
        public void ExistAsyncWithExistingBlockReturnsTrue()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/ExistAsyncWithExistingBlock");
            var block = new Block();

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Commit();
            }

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.ExistAsync(block.GetHash());
                task.Wait();

                Assert.True(task.Result);
            }
        }

        [Fact]
        public void ExistAsyncWithoutExistingBlockReturnsFalse()
        {
            var dir = AssureEmptyDir("TestData/BlockRepository/ExistAsyncWithoutExistingBlock");

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.ExistAsync(new uint256());
                task.Wait();

                Assert.False(task.Result);
            }
        }

        [Fact]
        public void DeleteAsyncRemovesBlocksAndTransactions()
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

            using (var repository = SetupRepository(Network.Main, dir))
            {
                var task = repository.DeleteAsync(new uint256(45), new List<uint256> { block.GetHash() });
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var trans = engine.GetTransaction();

                var blockHashKeyRow = trans.Select<byte[], uint256>("Common", new byte[0]);
                var blockDict = trans.SelectDictionary<byte[], byte[]>("Block");
                var transDict = trans.SelectDictionary<byte[], byte[]>("Transaction");

                Assert.Equal(new uint256(45), blockHashKeyRow.Value);
                Assert.Empty(blockDict);
                Assert.Empty(transDict);
            }
        }

        private BlockStore.IBlockRepository SetupRepository(Network main, string dir)
        {
            var repository = new BlockRepository(main, dir, DateTimeProvider.Default, this.loggerFactory);
            repository.InitializeAsync().GetAwaiter().GetResult();

            return repository;
        }
    }
}