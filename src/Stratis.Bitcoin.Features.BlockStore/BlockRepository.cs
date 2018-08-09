﻿using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Persist the next block hash and insert new blocks into the database.
        /// </summary>
        /// <param name="newTip">New repository's tip.</param>
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
        /// <param name="newTip">New repository's tip.</param>
        /// <param name="hashes">List of all block hashes to be deleted.</param>
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

        BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; } //TODO ACTIVATION consider removing

        bool TxIndex { get; }
    }

    public class BlockRepository : IBlockRepository
    {
        private const string BlockTableName = "Block";

        private const string TransactionTableName = "Transaction";

        private const string CommonTableName = "Common";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine DBreeze;

        private readonly Network network;

        private static readonly byte[] RepositoryTipKey = new byte[0];

        private static readonly byte[] TxIndexKey = new byte[1];

        public HashHeightPair TipHashAndHeight { get; private set; }

        public BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }

        public bool TxIndex { get; private set; }

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public BlockRepository(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(network, dataFolder.BlockPath, dateTimeProvider, loggerFactory)
        {
        }

        public BlockRepository(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.DBreeze = new DBreezeEngine(folder);
            this.network = network;
            this.dateTimeProvider = dateTimeProvider;

            this.PerformanceCounter = this.PerformanceCounterFactory();
        }

        public virtual BlockStoreRepositoryPerformanceCounter PerformanceCounterFactory()
        {
            return new BlockStoreRepositoryPerformanceCounter(this.dateTimeProvider);
        }

        /// <inheritdoc />
        public virtual Task InitializeAsync()
        {
            this.logger.LogTrace("()");
            Block genesis = this.network.GetGenesis();

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    bool doCommit = false;

                    if (this.LoadBlockHash(transaction) == null)
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

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
                return Task.FromResult(default(Transaction));

            Task<Transaction> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");
                Transaction res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], uint256> transactionRow = transaction.Select<byte[], uint256>(TransactionTableName, trxid.ToBytes());
                    if (!transactionRow.Exists)
                    {
                        this.PerformanceCounter.AddRepositoryMissCount(1);
                        this.logger.LogTrace("(-)[NO_BLOCK]:null");
                        return null;
                    }

                    this.PerformanceCounter.AddRepositoryHitCount(1);

                    Row<byte[], Block> blockRow = transaction.Select<byte[], Block>(BlockTableName, transactionRow.Value.ToBytes());
                    if (blockRow.Exists)
                        res = blockRow.Value.Transactions.FirstOrDefault(t => t.GetHash() == trxid);

                    if (res != null) this.PerformanceCounter.AddRepositoryHitCount(1);
                    else this.PerformanceCounter.AddRepositoryMissCount(1);
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
        {
            Guard.NotNull(trxid, nameof(trxid));
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);

            if (!this.TxIndex)
            {
                this.logger.LogTrace("(-)[NO_TXINDEX]:null");
                return Task.FromResult(default(uint256));
            }

            Task<uint256> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");
                uint256 res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    Row<byte[], uint256> transactionRow = transaction.Select<byte[], uint256>(TransactionTableName, trxid.ToBytes());
                    if (transactionRow.Exists)
                    {
                        res = transactionRow.Value;
                        this.PerformanceCounter.AddRepositoryHitCount(1);
                    }
                    else
                    {
                        this.PerformanceCounter.AddRepositoryMissCount(1);
                    }
                }

                this.logger.LogTrace("(-):'{0}'", res);
                return res;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        protected virtual void OnInsertBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, List<Block> blocks)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blocks), nameof(blocks.Count), blocks?.Count);

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
                Row<byte[], Block> blockRow = dbreezeTransaction.Select<byte[], Block>(BlockTableName, blockId.ToBytes());
                if (!blockRow.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    this.PerformanceCounter.AddRepositoryInsertCount(1);
                    dbreezeTransaction.Insert<byte[], Block>(BlockTableName, blockId.ToBytes(), block);

                    if (this.TxIndex)
                    {
                        foreach (Transaction transaction in block.Transactions)
                            transactions.Add((transaction, block));
                    }
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }
            }

            if (this.TxIndex)
                this.OnInsertTransactions(dbreezeTransaction, transactions);

            this.logger.LogTrace("(-)");
        }

        protected virtual void OnInsertTransactions(DBreeze.Transactions.Transaction dbreezeTransaction, List<(Transaction, Block)> transactions)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(transactions), nameof(transactions.Count), transactions?.Count);

            var byteListComparer = new ByteListComparer();
            transactions.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Item1.GetHash().ToBytes(), pair2.Item1.GetHash().ToBytes()));

            // Index transactions.
            foreach ((Transaction transaction, Block block) in transactions)
            {
                this.PerformanceCounter.AddRepositoryInsertCount(1);
                dbreezeTransaction.Insert<byte[], uint256>(TransactionTableName, transaction.GetHash().ToBytes(), block.GetHash());
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public Task ReIndexAsync()
        {
            this.logger.LogTrace("()");

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction dbreezeTransaction = this.DBreeze.GetTransaction())
                {
                    dbreezeTransaction.SynchronizeTables(BlockTableName, TransactionTableName);

                    if (this.TxIndex)
                    {
                        // Insert transactions to database.
                        IEnumerable<Row<byte[], Block>> blockRows = dbreezeTransaction.SelectForward<byte[], Block>(BlockTableName);
                        foreach (Row<byte[], Block> blockRow in blockRows)
                        {
                            foreach (Transaction transaction in blockRow.Value.Transactions)
                            {
                                dbreezeTransaction.Insert<byte[], uint256>(TransactionTableName, transaction.GetHash().ToBytes(), blockRow.Value.GetHash());
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

                this.logger.LogTrace("(-)");
                return Task.CompletedTask;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(HashHeightPair newTip, List<Block> blocks)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(blocks, nameof(blocks));
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(newTip), newTip, nameof(blocks), nameof(blocks.Count), blocks?.Count);

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

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

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        private bool? LoadTxIndex(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            this.logger.LogTrace("()");

            bool? res = null;
            Row<byte[], bool> row = dbreezeTransaction.Select<byte[], bool>(CommonTableName, TxIndexKey);
            if (row.Exists)
            {
                this.PerformanceCounter.AddRepositoryHitCount(1);
                this.TxIndex = row.Value;
                res = row.Value;
            }
            else
            {
                this.PerformanceCounter.AddRepositoryMissCount(1);
            }

            this.logger.LogTrace("(-):{0}", res);
            return res;
        }

        private void SaveTxIndex(DBreeze.Transactions.Transaction dbreezeTransaction, bool txIndex)
        {
            this.logger.LogTrace("({0}:{1})", nameof(txIndex), txIndex);

            this.TxIndex = txIndex;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
            dbreezeTransaction.Insert<byte[], bool>(CommonTableName, TxIndexKey, txIndex);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public Task SetTxIndexAsync(bool txIndex)
        {
            this.logger.LogTrace("({0}:{1})", nameof(txIndex), txIndex);

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    this.SaveTxIndex(transaction, txIndex);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        private HashHeightPair LoadBlockHash(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            this.logger.LogTrace("()");

            if (this.TipHashAndHeight == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], HashHeightPair> row = dbreezeTransaction.Select<byte[], HashHeightPair>(CommonTableName, RepositoryTipKey);
                if (row.Exists)
                    this.TipHashAndHeight = row.Value;

                dbreezeTransaction.ValuesLazyLoadingIsOn = true;
            }

            this.logger.LogTrace("(-):'{0}'", this.TipHashAndHeight);
            return this.TipHashAndHeight;
        }

        private void SaveTipHashAndHeight(DBreeze.Transactions.Transaction dbreezeTransaction, HashHeightPair newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            this.TipHashAndHeight = newTip;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
            dbreezeTransaction.Insert<byte[], HashHeightPair>(CommonTableName, RepositoryTipKey, this.TipHashAndHeight);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public Task<Block> GetBlockAsync(uint256 hash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(hash), hash);
            Guard.NotNull(hash, nameof(hash));

            Task<Block> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                Block res = null;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    byte[] key = hash.ToBytes();
                    Row<byte[], Block> blockRow = transaction.Select<byte[], Block>(BlockTableName, key);
                    if (blockRow.Exists)
                    {
                        res = blockRow.Value;
                        this.PerformanceCounter.AddRepositoryHitCount(1);
                    }
                    else
                    {
                        this.PerformanceCounter.AddRepositoryMissCount(1);
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<List<Block>> GetBlocksAsync(List<uint256> hashes)
        {
            Guard.NotNull(hashes, nameof(hashes));
            this.logger.LogTrace("({0}:{1})", nameof(hashes.Count), hashes.Count);

            Task<List<Block>> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                List<Block> blocks;

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    blocks = this.GetBlocksFromHashes(transaction, hashes);
                }

                this.logger.LogTrace("(-)");

                return blocks;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<bool> ExistAsync(uint256 hash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(hash), hash);
            Guard.NotNull(hash, nameof(hash));

            Task<bool> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                bool res = false;
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    // Lazy loading is on so we don't fetch the whole value, just the row.
                    byte[] key = hash.ToBytes();
                    Row<byte[], Block> blockRow = transaction.Select<byte[], Block>("Block", key);
                    if (blockRow.Exists)
                    {
                        this.PerformanceCounter.AddRepositoryHitCount(1);
                        res = true;
                    }
                    else
                    {
                        this.PerformanceCounter.AddRepositoryMissCount(1);
                    }
                }

                this.logger.LogTrace("(-):{0}", res);
                return res;
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        protected virtual void OnDeleteTransactions(DBreeze.Transactions.Transaction dbreezeTransaction, List<(Transaction, Block)> transactions)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(transactions), nameof(transactions.Count), transactions?.Count);

            foreach ((Transaction transaction, Block block) in transactions)
            {
                this.PerformanceCounter.AddRepositoryDeleteCount(1);
                dbreezeTransaction.RemoveKey<byte[]>(TransactionTableName, transaction.GetHash().ToBytes());
            }

            this.logger.LogTrace("(-)");
        }

        protected virtual void OnDeleteBlocks(DBreeze.Transactions.Transaction dbreezeTransaction, List<Block> blocks)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blocks), nameof(blocks.Count), blocks?.Count);

            if (this.TxIndex)
            {
                var transactions = new List<(Transaction, Block)>();

                foreach (Block block in blocks)
                    foreach (Transaction transaction in block.Transactions)
                        transactions.Add((transaction, block));

                this.OnDeleteTransactions(dbreezeTransaction, transactions);
            }

            foreach (Block block in blocks)
            {
                this.PerformanceCounter.AddRepositoryDeleteCount(1);
                dbreezeTransaction.RemoveKey<byte[]>(BlockTableName, block.GetHash().ToBytes());
            }

            this.logger.LogTrace("(-)");
        }

        private List<Block> GetBlocksFromHashes(DBreeze.Transactions.Transaction dbreezeTransaction, List<uint256> hashes)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(hashes), nameof(hashes.Count), hashes?.Count);

            var results = new Dictionary<uint256, Block>();

            // Access hash keys in sorted order.
            var byteListComparer = new ByteListComparer();
            List<(uint256, byte[])> keys = hashes.Select(hash => (hash, hash.ToBytes())).ToList();

            keys.Sort((key1, key2) => byteListComparer.Compare(key1.Item2, key2.Item2));

            foreach ((uint256, byte[]) key in keys)
            {
                Row<byte[], Block> blockRow = dbreezeTransaction.Select<byte[], Block>(BlockTableName, key.Item2);
                if (blockRow.Exists)
                {
                    results[key.Item1] = blockRow.Value;
                    this.PerformanceCounter.AddRepositoryHitCount(1);

                    this.logger.LogTrace("Block hash '{0}' loaded from the store.", key.Item1);
                }
                else
                {
                    results[key.Item1] = null;
                    this.PerformanceCounter.AddRepositoryMissCount(1);

                    this.logger.LogTrace("Block hash '{0}' not found in the store.", key.Item1);
                }
            }

            this.logger.LogTrace("(-):{0}", results.Count);

            // Return the result in the order that the hashes were presented.
            return hashes.Select(hash => results[hash]).ToList();
        }

        /// <inheritdoc />
        public Task DeleteAsync(HashHeightPair newTip, List<uint256> hashes)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(newTip), newTip, nameof(hashes), nameof(hashes.Count), hashes?.Count);
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(hashes, nameof(hashes));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockTableName, CommonTableName, TransactionTableName);
                    transaction.ValuesLazyLoadingIsOn = false;

                    List<Block> blocks = this.GetBlocksFromHashes(transaction, hashes);
                    this.OnDeleteBlocks(transaction, blocks.Where(b => b != null).ToList());
                    this.SaveTipHashAndHeight(transaction, newTip);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}
