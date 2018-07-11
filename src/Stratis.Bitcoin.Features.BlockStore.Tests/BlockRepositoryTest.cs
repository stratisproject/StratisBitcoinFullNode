using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockRepositoryTest : LogsTestBase
    {
        [Fact]
        public void InitializesGenBlockAndTxIndexOnFirstLoad()
        {
            string dir = CreateTestDir(this);
            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();

                Row<byte[], uint256> blockRow = transaction.Select<byte[], uint256>("Common", new byte[0]);
                Row<byte[], bool> txIndexRow = transaction.Select<byte[], bool>("Common", new byte[1]);

                Assert.Equal(Network.Main.GetGenesis().GetHash(), blockRow.Value);
                Assert.False(txIndexRow.Value);
            }
        }

        [Fact]
        public void DoesNotOverwriteExistingBlockAndTxIndexOnFirstLoad()
        {
            string dir = CreateTestDir(this);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();

                transaction.Insert<byte[], byte[]>("Common", new byte[0], new uint256(56).ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();

                Row<byte[], uint256> blockRow = transaction.Select<byte[], uint256>("Common", new byte[0]);
                Row<byte[], bool> txIndexRow = transaction.Select<byte[], bool>("Common", new byte[1]);

                Assert.Equal(new uint256(56), blockRow.Value);
                Assert.True(txIndexRow.Value);
            }
        }

        [Fact]
        public void GetTrxAsyncWithoutTransactionIndexReturnsNewTransaction()
        {
            string dir = CreateTestDir(this);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();

                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<Transaction> task = repository.GetTrxAsync(uint256.Zero);
                task.Wait();

                Assert.Equal(default(Transaction), task.Result);
            }
        }

        [Fact]
        public void GetTrxAsyncWithoutTransactionInIndexReturnsNull()
        {
            string dir = CreateTestDir(this);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                var blockId = new uint256(8920);
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<Transaction> task = repository.GetTrxAsync(new uint256(65));
                task.Wait();

                Assert.Null(task.Result);
            }
        }

        [Fact]
        public void GetTrxAsyncWithTransactionReturnsExistingTransaction()
        {
            string dir = CreateTestDir(this);
            Transaction trans = Network.Main.CreateTransaction();
            trans.Version = 125;

            using (var engine = new DBreezeEngine(dir))
            {
                Block block = Network.Main.Consensus.ConsensusFactory.CreateBlock();
                block.Header.GetHash();
                block.Transactions.Add(trans);

                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.Header.GetHash().ToBytes(), block.ToBytes());
                transaction.Insert<byte[], byte[]>("Transaction", trans.GetHash().ToBytes(), block.Header.GetHash().ToBytes());
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<Transaction> task = repository.GetTrxAsync(trans.GetHash());
                task.Wait();

                Assert.Equal((uint)125, task.Result.Version);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithoutTxIndexReturnsDefaultId()
        {
            string dir = CreateTestDir(this);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<uint256> task = repository.GetTrxBlockIdAsync(new uint256(26));
                task.Wait();

                Assert.Equal(default(uint256), task.Result);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithoutExistingTransactionReturnsNull()
        {
            string dir = CreateTestDir(this);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<uint256> task = repository.GetTrxBlockIdAsync(new uint256(26));
                task.Wait();

                Assert.Null(task.Result);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithTransactionReturnsBlockId()
        {
            string dir = CreateTestDir(this);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Transaction", new uint256(26).ToBytes(), new uint256(42).ToBytes());
                transaction.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<uint256> task = repository.GetTrxBlockIdAsync(new uint256(26));
                task.Wait();

                Assert.Equal(new uint256(42), task.Result);
            }
        }

        [Fact]
        public void PutAsyncWritesBlocksAndTransactionsToDbAndSavesNextBlockHash()
        {
            string dir = CreateTestDir(this);

            var nextBlockHash = new uint256(1241256);
            var blocks = new List<Block>();
            Block block = Network.Main.Consensus.ConsensusFactory.CreateBlock();
            BlockHeader blockHeader = block.Header;
            blockHeader.Bits = new Target(12);
            Transaction transaction = Network.Main.CreateTransaction();
            transaction.Version = 32;
            block.Transactions.Add(transaction);
            transaction = Network.Main.CreateTransaction();
            transaction.Version = 48;
            block.Transactions.Add(transaction);
            blocks.Add(block);
            
            Block block2 = Network.Main.Consensus.ConsensusFactory.CreateBlock();
            transaction = Network.Main.CreateTransaction();
            transaction.Version = 15;
            block2.Transactions.Add(transaction);
            blocks.Add(block2);

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();
                trans.Insert<byte[], byte[]>("Common", new byte[0], uint256.Zero.ToBytes());
                trans.Insert<byte[], bool>("Common", new byte[1], true);
                trans.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task task = repository.PutAsync(nextBlockHash, blocks);
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();

                Row<byte[], uint256> blockHashKeyRow = trans.Select<byte[], uint256>("Common", new byte[0]);
                Dictionary<byte[], byte[]> blockDict = trans.SelectDictionary<byte[], byte[]>("Block");
                Dictionary<byte[], byte[]> transDict = trans.SelectDictionary<byte[], byte[]>("Transaction");

                Assert.Equal(nextBlockHash, blockHashKeyRow.Value);
                Assert.Equal(2, blockDict.Count);
                Assert.Equal(3, transDict.Count);

                foreach (KeyValuePair<byte[], byte[]> item in blockDict)
                {
                    Block bl = blocks.Single(b => b.GetHash() == new uint256(item.Key));
                    Assert.Equal(bl.Header.GetHash(), Block.Load(item.Value, Network.Main).Header.GetHash());
                }

                foreach (KeyValuePair<byte[], byte[]> item in transDict)
                {
                    Block bl = blocks.Single(b => b.Transactions.Any(t => t.GetHash() == new uint256(item.Key)));
                    Assert.Equal(bl.GetHash(), new uint256(item.Value));
                }
            }
        }

        [Fact]
        public void SetTxIndexUpdatesTxIndex()
        {
            string dir = CreateTestDir(this);
            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();
                trans.Insert<byte[], bool>("Common", new byte[1], true);
                trans.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task task = repository.SetTxIndexAsync(false);
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();

                Row<byte[], bool> txIndexRow = trans.Select<byte[], bool>("Common", new byte[1]);
                Assert.False(txIndexRow.Value);
            }
        }

        [Fact]
        public void SetBlockHashUpdatesBlockHash()
        {
            string dir = CreateTestDir(this);
            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();
                trans.Insert<byte[], byte[]>("Common", new byte[0], new uint256(41).ToBytes());
                trans.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task task = repository.SetBlockHashAsync(new uint256(56));
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();

                Row<byte[], byte[]> blockHashKeyRow = trans.Select<byte[], byte[]>("Common", new byte[0]);
                Assert.Equal(new uint256(56), new uint256(blockHashKeyRow.Value));
            }
        }

        [Fact]
        public void GetAsyncWithExistingBlockReturnsBlock()
        {
            string dir = CreateTestDir(this);
            Block block = Network.Main.Consensus.ConsensusFactory.CreateBlock();

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<Block> task = repository.GetAsync(block.GetHash());
                task.Wait();

                Assert.Equal(block.GetHash(), task.Result.GetHash());
            }
        }

        [Fact]
        public void GetAsyncWithExistingBlocksReturnsBlocks()
        {
            string dir = CreateTestDir(this);
            var blocks = new Block[10];

            blocks[0] = Network.Main.Consensus.ConsensusFactory.CreateBlock();
            for (int i = 1; i < blocks.Length; i++)
            {
                blocks[i] = Network.Main.Consensus.ConsensusFactory.CreateBlock();
                blocks[i].Header.HashPrevBlock = blocks[i - 1].Header.GetHash();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                for (int i = 0; i < blocks.Length; i++)
                    transaction.Insert<byte[], byte[]>("Block", blocks[i].GetHash().ToBytes(), blocks[i].ToBytes());
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<List<Block>> task = repository.GetBlocksAsync(blocks.Select(b => b.GetHash()).ToList());
                task.Wait();

                Assert.Equal(blocks.Length, task.Result.Count);
                for (int i = 0; i < 10; i++)
                    Assert.Equal(blocks[i].GetHash(), task.Result[i].GetHash());
            }
        }

        [Fact]
        public void GetAsyncWithoutExistingBlockReturnsNull()
        {
            string dir = CreateTestDir(this);

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<Block> task = repository.GetAsync(new uint256());
                task.Wait();

                Assert.Null(task.Result);
            }
        }

        [Fact]
        public void ExistAsyncWithExistingBlockReturnsTrue()
        {
            string dir = CreateTestDir(this);
            Block block = Network.Main.Consensus.ConsensusFactory.CreateBlock();

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<bool> task = repository.ExistAsync(block.GetHash());
                task.Wait();

                Assert.True(task.Result);
            }
        }

        [Fact]
        public void ExistAsyncWithoutExistingBlockReturnsFalse()
        {
            string dir = CreateTestDir(this);

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task<bool> task = repository.ExistAsync(new uint256());
                task.Wait();

                Assert.False(task.Result);
            }
        }

        [Fact]
        public void DeleteAsyncRemovesBlocksAndTransactions()
        {
            string dir = CreateTestDir(this);
            Block block = Network.Main.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(Network.Main.CreateTransaction());

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction transaction = engine.GetTransaction();
                transaction.Insert<byte[], byte[]>("Block", block.GetHash().ToBytes(), block.ToBytes());
                transaction.Insert<byte[], byte[]>("Transaction", block.Transactions[0].GetHash().ToBytes(), block.GetHash().ToBytes());
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(Network.Main, dir))
            {
                Task task = repository.DeleteAsync(new uint256(45), new List<uint256> { block.GetHash() });
                task.Wait();
            }

            using (var engine = new DBreezeEngine(dir))
            {
                DBreeze.Transactions.Transaction trans = engine.GetTransaction();

                Row<byte[], uint256> blockHashKeyRow = trans.Select<byte[], uint256>("Common", new byte[0]);
                Dictionary<byte[], byte[]> blockDict = trans.SelectDictionary<byte[], byte[]>("Block");
                Dictionary<byte[], byte[]> transDict = trans.SelectDictionary<byte[], byte[]>("Transaction");

                Assert.Equal(new uint256(45), blockHashKeyRow.Value);
                Assert.Empty(blockDict);
                Assert.Empty(transDict);
            }
        }

        private IBlockRepository SetupRepository(Network main, string dir)
        {
            var repository = new BlockRepository(main, dir, DateTimeProvider.Default, this.LoggerFactory.Object);
            repository.InitializeAsync().GetAwaiter().GetResult();

            return repository;
        }
    }
}
