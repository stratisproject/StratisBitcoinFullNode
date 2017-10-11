using Stratis.Bitcoin;
using Stratis.Bitcoin.Tests;
using Stratis.Bitcoin.Features.IndexStore;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    using DBreeze;
    using Moq;
    using NBitcoin;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Newtonsoft.Json;

    public class IndexRepositoryTest : TestBase
    {
        [Fact]
        public async Task InitializesGenBlockAndTxIndexOnFirstLoad_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/InitializeGenBlockAndTxIndex");
            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
            }

            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                var blockRow = transaction.Select<byte[], uint256>("Common", new byte[0]);
                var txIndexRow = transaction.Select<byte[], bool>("Common", new byte[1]);

                Assert.Equal(Network.Main.GetGenesis().GetHash(), blockRow.Value);
                Assert.True(txIndexRow.Value);
            }
        }

        [Fact]
        public async Task DoesNotOverwriteExistingBlockAndTxIndexOnFirstLoad_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/NoOverwriteExistingBlockAndTxIndex");

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
        public async Task GetTrxAsyncWithoutTransactionIndexReturnsNewTransaction_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetTrxAsyncWithoutTxIndex");

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
        public async Task GetTrxAsyncWithoutTransactionInIndexReturnsNull_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetTrxAsyncWithoutTransactionFound");

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
        public async Task GetTrxAsyncWithTransactionReturnsExistingTransaction_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetTrxAsyncWithTransaction");
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
        public async Task GetTrxBlockIdAsyncWithoutTxIndexReturnsDefaultId_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetTrxBlockIdAsyncWithoutTxIndex");

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
        public async Task GetTrxBlockIdAsyncWithoutExistingTransactionReturnsNull_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetTrxBlockIdAsyncWithoutTransaction");

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
        public async Task GetTrxBlockIdAsyncWithTransactionReturnsBlockId_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetTrxBlockIdAsyncWithoutTransaction");

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
        public async Task PutAsyncWritesBlocksAndTransactionsToDbAndSavesNextBlockHash_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/PutAsyncStoresBlocksAndTxs");

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
        public async Task SetTxIndexUpdatesTxIndex_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/SetTxIndexUpdatesTxIndex");
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
        public async Task SetBlockHashUpdatesBlockHash_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/SetBlockHashUpdatesBlockHash");
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
        public async Task GetAsyncWithExistingBlockReturnsBlock_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetAsyncWithExistingBlock");
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
        public async Task GetAsyncWithoutExistingBlockReturnsNull_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/GetAsyncWithoutExistingBlock");

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.GetAsync(new uint256()).ConfigureAwait(false);

                Assert.Null(result);
            }
        }

        [Fact]
        public async Task ExistAsyncWithExistingBlockReturnsTrue_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/ExistAsyncWithExistingBlock");
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
        public async Task ExistAsyncWithoutExistingBlockReturnsFalse_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/ExistAsyncWithoutExistingBlock");

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                var result = await repository.ExistAsync(new uint256()).ConfigureAwait(false);

                Assert.False(result);
            }
        }

        [Fact]
        public async Task CreateIndexCreatesMultiValueIndexAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/CreateIndexCreatesMultiValueIndex");
            var block = new Block();
            var trans = new Transaction();
            Key key = new Key(); // generate a random private key
            var scriptPubKeyOut = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey); 
            trans.Outputs.Add(new TxOut(100, scriptPubKeyOut));
            block.Transactions.Add(trans);
            var hash = trans.GetHash().ToBytes();

            var block2 = new Block();
            block2.Header.HashPrevBlock = block.GetHash();
            var trans2 = new Transaction();
            Key key2 = new Key(); // generate a random private key
            var scriptPubKeyOut2 = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key2.PubKey); 
            trans2.Outputs.Add(new TxOut(200, scriptPubKeyOut2));
            block2.Transactions.Add(trans2);
            var hash2 = trans2.GetHash().ToBytes();

            var builder = "(t,b,n) => t.Outputs.Where(o => o.ScriptPubKey.GetDestinationAddress(n)!=null).Select((o, N) => new object[] { new uint160(o.ScriptPubKey.Hash.ToBytes()), new object[] { t.GetHash(), (uint)N } })";
            string indexTable;
            string expectedJSON;

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                await (repository as IndexRepository).SetTxIndex(true).ConfigureAwait(false);

                // Insert a block before creating the index
                await (repository as IndexRepository).PutAsync(block.GetHash(), new List<Block> { block }).ConfigureAwait(false);

                await repository.CreateIndex("Script", true, builder).ConfigureAwait(false);

                // Insert a block after creating the index
                await (repository as IndexRepository).PutAsync(block2.GetHash(), new List<Block> { block2 }).ConfigureAwait(false);

                var index = new Index(repository as IndexRepository, "Script", true, builder);                
                indexTable = index.Table;
                expectedJSON = index.ToString();
            }
            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                // Index has been recorded correctly in Common table?
                var indexKeyRow = transaction.Select<string, string>("Common", indexTable);
                Assert.True(indexKeyRow.Exists && indexKeyRow.Value != null);
                Assert.Equal(expectedJSON, indexKeyRow.Value);

                // Block has been indexed?
                var IndexedRow = transaction.Select<byte[], byte[]>(indexTable, trans.Outputs[0].ScriptPubKey.Hash.ToBytes());
                Assert.True(IndexedRow.Exists);
                // Correct value indexed?
                var compare = new byte[64];
                compare[3] = 38;
                compare[5] = 36;
                hash.CopyTo(compare, 6);
                Assert.True((new Index.Comparer()).Equals(compare, IndexedRow.Value));
                
                // Block2 has been indexed?
                var IndexedRow2 = transaction.Select<byte[], byte[]>(indexTable, trans2.Outputs[0].ScriptPubKey.Hash.ToBytes());
                Assert.True(IndexedRow2.Exists);
                // Correct value indexed?
                var compare2 = new byte[64];
                compare2[3] = 38;
                compare2[5] = 36;
                hash2.CopyTo(compare2, 6);
                Assert.True((new Index.Comparer()).Equals(compare2, IndexedRow2.Value));
            }
        }

        [Fact]
        public async Task CreateIndexCreatesSingleValueIndexAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/CreateIndexCreatesSingleValueIndex");

            // Transaction has outputs
            var block = new Block();
            var trans = new Transaction();
            Key key = new Key(); // generate a random private key
            var scriptPubKeyOut = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey);
            trans.Outputs.Add(new TxOut(100, scriptPubKeyOut));
            block.Transactions.Add(trans);
            var hash = trans.GetHash().ToBytes();

            // Transaction has inputs (i.PrevOut)
            var block2 = new Block();
            block2.Header.HashPrevBlock = block.GetHash();
            var trans2 = new Transaction();
            trans2.Inputs.Add(new TxIn(new OutPoint(trans, 0)));
            block2.Transactions.Add(trans2);
            var hash2 = trans2.GetHash().ToBytes();

            var builder = "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })";
            string indexTable;
            string expectedJSON;

            using (var repository = await SetupRepositoryAsync(Network.Main, dir).ConfigureAwait(false))
            {
                await (repository as IndexRepository).SetTxIndex(true).ConfigureAwait(false);

                // Insert a block before creating the index
                await (repository as IndexRepository).PutAsync(block.GetHash(), new List<Block> { block }).ConfigureAwait(false);

                await repository.CreateIndex("Output", false, builder).ConfigureAwait(false);

                // Insert a block after creating the index
                await (repository as IndexRepository).PutAsync(block2.GetHash(), new List<Block> { block2 }).ConfigureAwait(false);

                var index = new Index(repository as IndexRepository, "Output", false, builder);
                indexTable = index.Table;
                expectedJSON = index.ToString();
            }
            using (var engine = new DBreezeEngine(dir))
            {
                var transaction = engine.GetTransaction();

                var indexKeyRow = transaction.Select<string, string>("Common", indexTable);
                Assert.True(indexKeyRow.Exists && indexKeyRow.Value != null);
                Assert.Equal(expectedJSON, indexKeyRow.Value);

                // Block2 has been indexed?
                var indexKey2 = hash.Concat(new byte[] { 0, 0, 0, 0 }).ToArray();
                var IndexedRow2 = transaction.Select<byte[], byte[]>(indexTable, indexKey2);
                Assert.True(IndexedRow2.Exists);
                // Correct value indexed?
                var compare2 = new byte[32];
                hash2.CopyTo(compare2, 0);
                Assert.Equal(compare2, IndexedRow2.Value);
            }
        }

        [Fact]
        public async Task DeleteAsyncRemovesBlocksAndTransactions_IXAsync()
        {
            var dir = AssureEmptyDir("TestData/IndexRepository/DeleteAsyncRemovesBlocksAndTransactions");
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

        private async Task<Features.IndexStore.IIndexRepository> SetupRepositoryAsync(Network main, string dir)
        {
            var repository = new IndexRepository(main, dir, this.loggerFactory);
            await repository.Initialize().ConfigureAwait(false);

            return repository;
        }
    }
}