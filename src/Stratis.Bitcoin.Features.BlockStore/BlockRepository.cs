using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.DB;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// <see cref="IBlockRepository"/> is the interface to all the logics interacting with the blocks stored in the database.
    /// </summary>
    public interface IBlockRepository : IBlockStore
    {
        /// <summary> The database engine.</summary>
        IStratisDB StratisDB { get; }

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
        internal class BlockRepositorySerializer : IStratisDBSerializer
        {
            private readonly DBreezeSerializer dBreezeSerializer;

            public BlockRepositorySerializer(DBreezeSerializer dBreezeSerializer)
            {
                this.dBreezeSerializer = dBreezeSerializer;
            }

            public object Deserialize(byte[] objBytes, System.Type objType)
            {
                if (objType == typeof(byte[]))
                    return objBytes;

                return this.dBreezeSerializer.Deserialize(objBytes, objType);
            }

            public byte[] Serialize(object obj)
            {
                if (obj.GetType() == typeof(byte[]))
                    return (byte[])obj;

                return this.dBreezeSerializer.Serialize(obj);
            }
        }

        internal const string BlockTableName = "Block";

        internal const string CommonTableName = "Common";

        internal const string TransactionTableName = "Transaction";

        public IStratisDB StratisDB { get; }

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
            ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer, IStratisDBFactory stratisDBFactory = null)
            : this(network, dataFolder.BlockPath, loggerFactory, dBreezeSerializer, stratisDBFactory)
        {
        }

        public BlockRepository(Network network, string folder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer, IStratisDBFactory stratisDBFactory = null)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            Directory.CreateDirectory(folder);

            stratisDBFactory = stratisDBFactory ?? new StratisDBFactory();
            this.StratisDB = stratisDBFactory.CreateDatabase(folder, new BlockRepositorySerializer(dBreezeSerializer));

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
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite))
                {
                    bool doCommit = false;

                    if (this.LoadTipHashAndHeight(dbTransaction) == null)
                    {
                        this.SaveTipHashAndHeight(dbTransaction, new HashHeightPair(genesis.GetHash(), 0));
                        doCommit = true;
                    }

                    if (this.LoadTxIndex(dbTransaction) == null)
                    {
                        this.SaveTxIndex(dbTransaction, false);
                        doCommit = true;
                    }

                    if (doCommit) dbTransaction.Commit();
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
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.Read))
                {
                    if (!dbTransaction.Select(TransactionTableName, trxid, out uint256 blockHash))
                    {
                        this.logger.LogTrace("(-)[NO_BLOCK]:null");
                        return null;
                    }

                    if (dbTransaction.Select(BlockTableName, blockHash, out Block block))
                    {
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
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.Read))
                {
                    if (dbTransaction.Select(TransactionTableName, trxid, out uint256 blockHash))
                        res = blockHash;
                }

                return res;
            });

            return task;
        }

        protected virtual void OnInsertBlocks(IStratisDBTransaction dbTransaction, List<Block> blocks)
        {
            var transactions = new List<(Transaction, Block)>();

            bool[] blockExists = dbTransaction.ExistsMultiple<uint256>(BlockTableName, blocks.Select(b => b.GetHash()).ToArray());

            blocks = blocks.Where((b, n) => !blockExists[n]).ToList();

            dbTransaction.InsertMultiple(BlockTableName, blocks.Select(b => (b.GetHash(), b)).ToArray());

            // Index blocks.
            if (this.TxIndex)
            {
                foreach (Block block in blocks)
                {
                    foreach (Transaction transaction in block.Transactions)
                        transactions.Add((transaction, block));
                }

                this.OnInsertTransactions(dbTransaction, transactions);
            }
        }

        protected virtual void OnInsertTransactions(IStratisDBTransaction dbTransaction, List<(Transaction, Block)> transactions)
        {
            dbTransaction.InsertMultiple(TransactionTableName, transactions.Select(tb => (tb.Item1.GetHash(), tb.Item2.GetHash())).ToArray());
        }

        /// <inheritdoc />
        public Task ReIndexAsync()
        {
            Task task = Task.Run(() =>
            {
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite, BlockTableName, TransactionTableName))
                {
                    if (this.TxIndex)
                    {
                        // Insert transactions to database.
                        foreach ((uint256 blockHash, Block block) in dbTransaction.SelectForward<uint256, Block>(BlockTableName))
                        {
                            dbTransaction.InsertMultiple(TransactionTableName, block.Transactions.Select(t => (t.GetHash(), blockHash)).ToArray());
                        }
                    }
                    else
                    {
                        // Clear tx from database.
                        dbTransaction.RemoveAllKeys(TransactionTableName);
                    }

                    dbTransaction.Commit();
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
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite, BlockTableName, TransactionTableName, CommonTableName))
                {
                    this.OnInsertBlocks(dbTransaction, blocks);

                    // Commit additions
                    this.SaveTipHashAndHeight(dbTransaction, newTip);
                    dbTransaction.Commit();
                }
            });

            return task;
        }

        private bool? LoadTxIndex(IStratisDBTransaction dbTransaction)
        {
            bool? res = null;
            if (dbTransaction.Select(CommonTableName, TxIndexKey, out bool txIndex))
            {
                this.TxIndex = txIndex;
                res = txIndex;
            }

            return res;
        }

        private void SaveTxIndex(IStratisDBTransaction dbTransaction, bool txIndex)
        {
            this.TxIndex = txIndex;
            dbTransaction.Insert(CommonTableName, TxIndexKey, txIndex);
        }

        /// <inheritdoc />
        public Task SetTxIndexAsync(bool txIndex)
        {
            Task task = Task.Run(() =>
            {
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite))
                {
                    this.SaveTxIndex(dbTransaction, txIndex);
                    dbTransaction.Commit();
                }
            });

            return task;
        }

        private HashHeightPair LoadTipHashAndHeight(IStratisDBTransaction dbTransaction)
        {
            if (this.TipHashAndHeight == null && dbTransaction.Select(CommonTableName, RepositoryTipKey, out HashHeightPair hashHeightPair))
                this.TipHashAndHeight = hashHeightPair;

            return this.TipHashAndHeight;
        }

        private void SaveTipHashAndHeight(IStratisDBTransaction dbTransaction, HashHeightPair newTip)
        {
            this.TipHashAndHeight = newTip;
            dbTransaction.Insert(CommonTableName, RepositoryTipKey, newTip);
        }

        /// <inheritdoc />
        public Task<Block> GetBlockAsync(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            Task<Block> task = Task.Run(() =>
            {
                Block res = null;
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.Read))
                {
                    if (dbTransaction.Select(BlockTableName, hash, out Block block))
                        res = block;
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

                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.Read))
                {
                    blocks = this.GetBlocksFromHashes(dbTransaction, hashes);
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
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.Read))
                {
                    // Lazy loading is on so we don't fetch the whole value, just the row.
                    if (dbTransaction.Exists(BlockTableName, hash))
                        res = true;
                }

                return res;
            });

            return task;
        }

        protected virtual void OnDeleteTransactions(IStratisDBTransaction dbTransaction, List<(Transaction, Block)> transactions)
        {
            foreach ((Transaction transaction, Block block) in transactions)
                dbTransaction.RemoveKey(TransactionTableName, transaction.GetHash(), transaction);
        }

        protected virtual void OnDeleteBlocks(IStratisDBTransaction dbTransaction, List<Block> blocks)
        {
            if (this.TxIndex)
            {
                var transactions = new List<(Transaction, Block)>();

                foreach (Block block in blocks)
                    foreach (Transaction transaction in block.Transactions)
                        transactions.Add((transaction, block));

                this.OnDeleteTransactions(dbTransaction, transactions);
            }

            foreach (Block block in blocks)
                dbTransaction.RemoveKey(BlockTableName, block.GetHash(), block);
        }

        public List<Block> GetBlocksFromHashes(IStratisDBTransaction dbTransaction, List<uint256> hashes)
        {
            return dbTransaction.SelectMultiple<uint256, Block>(BlockTableName, hashes.ToArray());
        }

        /// <inheritdoc />
        public Task DeleteAsync(HashHeightPair newTip, List<uint256> hashes)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(hashes, nameof(hashes));

            Task task = Task.Run(() =>
            {
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite, BlockTableName, CommonTableName, TransactionTableName))
                {
                    List<Block> blocks = this.GetBlocksFromHashes(dbTransaction, hashes);
                    this.OnDeleteBlocks(dbTransaction, blocks.Where(b => b != null).ToList());
                    this.SaveTipHashAndHeight(dbTransaction, newTip);
                    dbTransaction.Commit();
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
                using (IStratisDBTransaction dbTransaction = this.StratisDB.CreateTransaction(StratisDBTransactionMode.ReadWrite, BlockTableName, CommonTableName, TransactionTableName))
                {
                    List<Block> blocks = this.GetBlocksFromHashes(dbTransaction, hashes);

                    this.OnDeleteBlocks(dbTransaction, blocks.Where(b => b != null).ToList());

                    dbTransaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.StratisDB.Dispose();
        }
    }
}
