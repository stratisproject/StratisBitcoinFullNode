using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// <see cref="IBlockRepository"/> is the interface to all the logics interacting with the blocks stored in the database.
    /// </summary>
    public interface IBlockRepository : IBlockStore
    {
        /// <summary> The dbreeze database engine.</summary>
        DBreezeEngine DBreeze { get; }

        /// <summary>
        /// Deletes blocks and indexes for transactions that belong to deleted blocks.
        /// <para>
        /// It should be noted that this does not delete the entries from disk (only the references are removed) and
        /// as such the file size remains the same.
        /// </para>
        /// </summary>
        /// <remarks>TODO: This will need to be revisited once DBreeze has been fixed or replaced with a solution that works.</remarks>
        /// <param name="hashes">List of block hashes to be deleted.</param>
        Task DeleteBlocksAsync(List<uint256> hashes);

        /// <summary>
        /// Persist the next block hash and insert new blocks into the database.
        /// </summary>
        /// <param name="newTip">Hash and height of the new repository's tip.</param>
        /// <param name="blocks">Blocks to be inserted.</param>
        Task PutAsync(HashHeightPair newTip, List<Block> blocks);

        /// <summary>
        /// Get the blocks from the database by using block hashes.
        /// </summary>
        /// <param name="hashes">A list of unique block hashes.</param>
        /// <returns>The blocks (or null if not found) in the same order as the hashes on input.</returns>
        Task<List<Block>> GetBlocksAsync(List<uint256> hashes);

        /// <summary>
        /// Wipe out blocks and their transactions then replace with a new block.
        /// </summary>
        /// <param name="newTip">Hash and height of the new repository's tip.</param>
        /// <param name="hashes">List of all block hashes to be deleted.</param>
        /// <exception cref="DBreeze.Exceptions.DBreezeException">Thrown if an error occurs during database operations.</exception>
        Task DeleteAsync(HashHeightPair newTip, List<uint256> hashes);

        /// <summary>
        /// Determine if a block already exists
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns><c>true</c> if the block hash can be found in the database, otherwise return <c>false</c>.</returns>
        Task<bool> ExistAsync(uint256 hash);

        /// <summary>
        /// Iterate over every block in the database.
        /// If <see cref="TxIndex"/> is true, we store the block hash alongside the transaction hash in the transaction table, otherwise clear the transaction table.
        /// </summary>
        Task ReIndexAsync();

        /// <summary>
        /// Set whether to index transactions by block hash, as well as storing them inside of the block.
        /// </summary>
        /// <param name="txIndex">Whether to index transactions.</param>
        Task SetTxIndexAsync(bool txIndex);

        /// <summary>Hash and height of the repository's tip.</summary>
        HashHeightPair TipHashAndHeight { get; }

        /// <summary> Indicates that the node should store all transaction data in the database.</summary>
        bool TxIndex { get; }
    }

    public class BlockRepository : IBlockRepository
    {
        internal const string BlockTableName = "Block";

        internal const string CommonTableName = "Common";

        internal const string TransactionTableName = "Transaction";

        public DBreezeEngine DBreeze { get; }

        private readonly ILogger logger;

        private readonly Network network;

        private static readonly byte[] RepositoryTipKey = new byte[0];

        private static readonly byte[] TxIndexKey = new byte[1];

        /// <inheritdoc />
        public HashHeightPair TipHashAndHeight { get; private set; }

        /// <inheritdoc />
        public bool TxIndex { get; private set; }

        private readonly DBreezeSerializer dBreezeSerializer;

        public BlockRepository(Network network, DataFolder dataFolder,
            ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer)
            : this(network, dataFolder.BlockPath, loggerFactory, dBreezeSerializer)
        {
        }

        public BlockRepository(Network network, string folder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            Directory.CreateDirectory(folder);
            this.DBreeze = new DBreezeEngine(folder);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.dBreezeSerializer = dBreezeSerializer;
        }

        /// <inheritdoc />
        public virtual Task InitializeAsync()
        {
            Block genesis = this.network.GetGenesis();

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    bool doCommit = false;

                    if (this.LoadTipHashAndHeight(transaction) == null)
                    {
                        this.SaveTipHashAndHeight(transaction, new HashHeightPair(genesis.GetHash(), 0));
                        doCommit = true;
                    }

                    if (this.LoadTxIndex(transaction) == null)
                    {
                        this.SaveTxIndex(transaction, false);
                        doCommit = true;
                    }

                    if (doCommit) transaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task<Transaction> GetTransactionByIdAsync(uint256 trxid)
        {
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
                return Task.FromResult(default(Transaction));

            Task<Transaction> task = Task.Run(() =>
            {
                Transaction res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], byte[]> transactionRow = transaction.Select<byte[], byte[]>(TransactionTableName, trxid.ToBytes());
                    if (!transactionRow.Exists)
                    {
                        this.logger.LogTrace("(-)[NO_BLOCK]:null");
                        return null;
                    }

                    Row<byte[], byte[]> blockRow = transaction.Select<byte[], byte[]>(BlockTableName, transactionRow.Value);

                    if (blockRow.Exists)
                    {
                        var block = this.dBreezeSerializer.Deserialize<Block>(blockRow.Value);
                        res = block.Transactions.FirstOrDefault(t => t.GetHash() == trxid);
                    }
                }

                return res;
            });

            return task;
        }

        /// <inheritdoc />
        public Task<uint256> GetBlockIdByTransactionIdAsync(uint256 trxid)
        {
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
            {
                this.logger.LogTrace("(-)[NO_TXINDEX]:null");
                return Task.FromResult(default(uint256));
            }

            Task<uint256> task = Task.Run(() =>
            {
                uint256 res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], byte[]> transactionRow = transaction.Select<byte[], byte[]>(TransactionTableName, trxid.ToBytes());
                    if (transactionRow.Exists)
                        res = new uint256(transactionRow.Value);
                }

                return res;
            });

            return task;
        }

        protected virtual void OnInsertBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, List<Block> blocks)
        {
            var transactions = new List<(Transaction, Block)>();
            var byteListComparer = new ByteListComparer();
            var blockDict = new Dictionary<uint256, Block>();

            // Gather blocks.
            foreach (Block block in blocks)
            {
                uint256 blockId = block.GetHash();
                blockDict[blockId] = block;
            }

            // Sort blocks. Be consistent in always converting our keys to byte arrays using the ToBytes method.
            List<KeyValuePair<uint256, Block>> blockList = blockDict.ToList();
            blockList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

            // Index blocks.
            foreach (KeyValuePair<uint256, Block> kv in blockList)
            {
                uint256 blockId = kv.Key;
                Block block = kv.Value;

                // If the block is already in store don't write it again.
                Row<byte[], byte[]> blockRow = dbreezeTransaction.Select<byte[], byte[]>(BlockTableName, blockId.ToBytes());
                if (!blockRow.Exists)
                {
                    dbreezeTransaction.Insert<byte[], byte[]>(BlockTableName, blockId.ToBytes(), this.dBreezeSerializer.Serialize(block));

                    if (this.TxIndex)
                    {
                        foreach (Transaction transaction in block.Transactions)
                            transactions.Add((transaction, block));
                    }
                }
            }

            if (this.TxIndex)
                this.OnInsertTransactions(dbreezeTransaction, transactions);
        }

        protected virtual void OnInsertTransactions(DBreeze.Transactions.Transaction dbreezeTransaction, List<(Transaction, Block)> transactions)
        {
            var byteListComparer = new ByteListComparer();
            transactions.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Item1.GetHash().ToBytes(), pair2.Item1.GetHash().ToBytes()));

            // Index transactions.
            foreach ((Transaction transaction, Block block) in transactions)
                dbreezeTransaction.Insert(TransactionTableName, transaction.GetHash().ToBytes(), block.GetHash().ToBytes());
        }

        /// <inheritdoc />
        public Task ReIndexAsync()
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(BlockTableName, TransactionTableName);

                    if (this.TxIndex)
                    {
                        // Insert transactions to database.
                        IEnumerable<Row<byte[], byte[]>> blockRows = dbreezeTransaction.SelectForward<byte[], byte[]>(BlockTableName);
                        foreach (Row<byte[], byte[]> blockRow in blockRows)
                        {
                            var block = this.dBreezeSerializer.Deserialize<Block>(blockRow.Value);
                            foreach (Transaction transaction in block.Transactions)
                            {
                                dbreezeTransaction.Insert<byte[], byte[]>(TransactionTableName, transaction.GetHash().ToBytes(), block.GetHash().ToBytes());
                            }
                        }
                    }
                    else
                    {
                        // Clear tx from database.
                        dbreezeTransaction.RemoveAllKeys(TransactionTableName, true);
                    }

                    dbreezeTransaction.Commit();
                }

                return Task.CompletedTask;
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(HashHeightPair newTip, List<Block> blocks)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(blocks, nameof(blocks));

            Task task = Task.Run(() =>
            {
                // DBreeze is faster if sort ascending by key in memory before insert
                // however we need to find how byte arrays are sorted in DBreeze.
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockTableName, TransactionTableName);
                    this.OnInsertBlocks(transaction, blocks);

                    // Commit additions
                    this.SaveTipHashAndHeight(transaction, newTip);
                    transaction.Commit();
                }
            });

            return task;
        }

        private bool? LoadTxIndex(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            bool? res = null;
            Row<byte[], bool> row = dbreezeTransaction.Select<byte[], bool>(CommonTableName, TxIndexKey);
            if (row.Exists)
            {
                this.TxIndex = row.Value;
                res = row.Value;
            }

            return res;
        }

        private void SaveTxIndex(DBreeze.Transactions.Transaction dbreezeTransaction, bool txIndex)
        {
            this.TxIndex = txIndex;
            dbreezeTransaction.Insert<byte[], bool>(CommonTableName, TxIndexKey, txIndex);
        }

        /// <inheritdoc />
        public Task SetTxIndexAsync(bool txIndex)
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    this.SaveTxIndex(transaction, txIndex);
                    transaction.Commit();
                }
            });

            return task;
        }

        private HashHeightPair LoadTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            if (this.TipHashAndHeight == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], byte[]> row = dbreezeTransaction.Select<byte[], byte[]>(CommonTableName, RepositoryTipKey);
                if (row.Exists)
                    this.TipHashAndHeight = this.dBreezeSerializer.Deserialize<HashHeightPair>(row.Value);

                dbreezeTransaction.ValuesLazyLoadingIsOn = true;
            }

            return this.TipHashAndHeight;
        }

        private void SaveTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction, HashHeightPair newTip)
        {
            this.TipHashAndHeight = newTip;
            dbreezeTransaction.Insert(CommonTableName, RepositoryTipKey, this.dBreezeSerializer.Serialize(newTip));
        }

        /// <inheritdoc />
        public Task<Block> GetBlockAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task<Block> task = Task.Run(() =>
            {
                Block res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    byte[] key = hash.ToBytes();
                    Row<byte[], byte[]> blockRow = transaction.Select<byte[], byte[]>(BlockTableName, key);
                    if (blockRow.Exists)
                        res = this.dBreezeSerializer.Deserialize<Block>(blockRow.Value);
                }

                // If searching for genesis block, return it.
                if (res == null && hash == this.network.GenesisHash)
                {
                    res = this.network.GetGenesis();
                }

                return res;
            });

            return task;
        }

        /// <inheritdoc />
        public Task<List<Block>> GetBlocksAsync(List<uint256> hashes)
        {
            Guard.NotNull(hashes, nameof(hashes));

            Task<List<Block>> task = Task.Run(() =>
            {
                List<Block> blocks;

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    blocks = this.GetBlocksFromHashes(transaction, hashes);
                }

                return blocks;
            });

            return task;
        }

        /// <inheritdoc />
        public Task<bool> ExistAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task<bool> task = Task.Run(() =>
            {
                bool res = false;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    // Lazy loading is on so we don't fetch the whole value, just the row.
                    byte[] key = hash.ToBytes();
                    Row<byte[], byte[]> blockRow = transaction.Select<byte[], byte[]>("Block", key);
                    if (blockRow.Exists)
                        res = true;
                }

                return res;
            });

            return task;
        }

        protected virtual void OnDeleteTransactions(DBreeze.Transactions.Transaction dbreezeTransaction, List<(Transaction, Block)> transactions)
        {
            foreach ((Transaction transaction, Block block) in transactions)
                dbreezeTransaction.RemoveKey<byte[]>(TransactionTableName, transaction.GetHash().ToBytes());
        }

        protected virtual void OnDeleteBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, List<Block> blocks)
        {
            if (this.TxIndex)
            {
                var transactions = new List<(Transaction, Block)>();

                foreach (Block block in blocks)
                    foreach (Transaction transaction in block.Transactions)
                        transactions.Add((transaction, block));

                this.OnDeleteTransactions(dbreezeTransaction, transactions);
            }

            foreach (Block block in blocks)
                dbreezeTransaction.RemoveKey<byte[]>(BlockTableName, block.GetHash().ToBytes());
        }

        public List<Block> GetBlocksFromHashes(DBreeze.Transactions.Transaction dbreezeTransaction, List<uint256> hashes)
        {
            var results = new Dictionary<uint256, Block>();

            // Access hash keys in sorted order.
            var byteListComparer = new ByteListComparer();
            List<(uint256, byte[])> keys = hashes.Select(hash => (hash, hash.ToBytes())).ToList();

            keys.Sort((key1, key2) => byteListComparer.Compare(key1.Item2, key2.Item2));

            foreach ((uint256, byte[]) key in keys)
            {
                Row<byte[], byte[]> blockRow = dbreezeTransaction.Select<byte[], byte[]>(BlockTableName, key.Item2);
                if (blockRow.Exists)
                {
                    results[key.Item1] = this.dBreezeSerializer.Deserialize<Block>(blockRow.Value);

                    this.logger.LogTrace("Block hash '{0}' loaded from the store.", key.Item1);
                }
                else
                {
                    results[key.Item1] = null;

                    this.logger.LogTrace("Block hash '{0}' not found in the store.", key.Item1);
                }
            }

            // Return the result in the order that the hashes were presented.
            return hashes.Select(hash => results[hash]).ToList();
        }

        /// <inheritdoc />
        public Task DeleteAsync(HashHeightPair newTip, List<uint256> hashes)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(hashes, nameof(hashes));

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockTableName, CommonTableName, TransactionTableName);
                    transaction.ValuesLazyLoadingIsOn = false;

                    List<Block> blocks = this.GetBlocksFromHashes(transaction, hashes);
                    this.OnDeleteBlocks(transaction, blocks.Where(b => b != null).ToList());
                    this.SaveTipHashAndHeight(transaction, newTip);
                    transaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task DeleteBlocksAsync(List<uint256> hashes)
        {
            Guard.NotNull(hashes, nameof(hashes));

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockRepository.BlockTableName, BlockRepository.CommonTableName, BlockRepository.TransactionTableName);
                    transaction.ValuesLazyLoadingIsOn = false;

                    List<Block> blocks = this.GetBlocksFromHashes(transaction, hashes);

                    this.OnDeleteBlocks(transaction, blocks.Where(b => b != null).ToList());

                    transaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}
