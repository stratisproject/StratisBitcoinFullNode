using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockRepository : IDisposable
    {
        Task InitializeAsync();
        Task PutAsync(uint256 nextBlockHash, List<Block> blocks);
        Task<Block> GetAsync(uint256 hash);
        Task<Transaction> GetTrxAsync(uint256 trxid);
        Task DeleteAsync(uint256 newlockHash, List<uint256> hashes);
        Task<bool> ExistAsync(uint256 hash);
        Task<uint256> GetTrxBlockIdAsync(uint256 trxid);
        Task SetBlockHashAsync(uint256 nextBlockHash);
        Task SetTxIndexAsync(bool txIndex);
        uint256 BlockHash { get; }
        BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }
        bool TxIndex { get; }
    }

    public class BlockRepository : IBlockRepository
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        protected readonly DBreezeEngine DBreeze;
        protected readonly Network network;

        protected static readonly byte[] BlockHashKey = new byte[0];
        protected static readonly byte[] TxIndexKey = new byte[1];

        public uint256 BlockHash { get; private set; }
        public BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }
        public bool TxIndex { get; private set; }

        /// <summary>Provider of time functions.</summary>
        protected readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Represents the last block stored to disk.</summary>
        public ChainedBlock HighestPersistedBlock { get; internal set; }

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

            this.PerformanceCounter = PerformanceCounterFactory();
        }

        public virtual BlockStoreRepositoryPerformanceCounter PerformanceCounterFactory()
        {
            return new BlockStoreRepositoryPerformanceCounter(this.dateTimeProvider);
        }

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
                        this.SaveBlockHash(transaction, genesis.GetHash());
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

                    Row<byte[], uint256> transactionRow = transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());
                    if (!transactionRow.Exists)
                    {
                        this.PerformanceCounter.AddRepositoryMissCount(1);
                        this.logger.LogTrace("(-)[NO_BLOCK]:null");
                        return null;
                    }

                    this.PerformanceCounter.AddRepositoryHitCount(1);

                    Row<byte[], Block> blockRow = transaction.Select<byte[], Block>("Block", transactionRow.Value.ToBytes());
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

                    Row<byte[], uint256> transactionRow = transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());
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
                Row<byte[], Block> blockRow = dbreezeTransaction.Select<byte[], Block>("Block", blockId.ToBytes());
                if (!blockRow.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    this.PerformanceCounter.AddRepositoryInsertCount(1);
                    dbreezeTransaction.Insert<byte[], Block>("Block", blockId.ToBytes(), block);

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
                dbreezeTransaction.Insert<byte[], uint256>("Transaction", transaction.GetHash().ToBytes(), block.GetHash());
            }

            this.logger.LogTrace("(-)");
        }

        public Task PutAsync(uint256 nextBlockHash, List<Block> blocks)
        {
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(blocks, nameof(blocks));
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(nextBlockHash), nextBlockHash, nameof(blocks), nameof(blocks.Count), blocks?.Count);

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                // DBreeze is faster if sort ascending by key in memory before insert
                // however we need to find how byte arrays are sorted in DBreeze.
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("Block", "Transaction");
                    this.OnInsertBlocks(transaction, blocks);

                    // Commit additions
                    this.SaveBlockHash(transaction, nextBlockHash);
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
            Row<byte[], bool> row = dbreezeTransaction.Select<byte[], bool>("Common", TxIndexKey);
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

        protected void SaveTxIndex(DBreeze.Transactions.Transaction dbreezeTransaction, bool txIndex)
        {
            this.logger.LogTrace("({0}:{1})", nameof(txIndex), txIndex);

            this.TxIndex = txIndex;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
            dbreezeTransaction.Insert<byte[], bool>("Common", TxIndexKey, txIndex);

            this.logger.LogTrace("(-)");
        }

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

        private uint256 LoadBlockHash(DBreeze.Transactions.Transaction dbreezeTransaction)
        {
            this.logger.LogTrace("()");

            if (this.BlockHash == null)
            {
                dbreezeTransaction.ValuesLazyLoadingIsOn = false;

                Row<byte[], uint256> row = dbreezeTransaction.Select<byte[], uint256>("Common", BlockHashKey);
                if (row.Exists)
                    this.BlockHash = row.Value;

                dbreezeTransaction.ValuesLazyLoadingIsOn = true;
            }

            this.logger.LogTrace("(-):'{0}'", this.BlockHash);
            return this.BlockHash;
        }

        public Task SetBlockHashAsync(uint256 nextBlockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(nextBlockHash), nextBlockHash);
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    this.SaveBlockHash(transaction, nextBlockHash);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        private void SaveBlockHash(DBreeze.Transactions.Transaction dbreezeTransaction, uint256 nextBlockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(nextBlockHash), nextBlockHash);

            this.BlockHash = nextBlockHash;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
            dbreezeTransaction.Insert<byte[], uint256>("Common", BlockHashKey, nextBlockHash);

            this.logger.LogTrace("(-)");
        }

        public Task<Block> GetAsync(uint256 hash)
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
                    Row<byte[], Block> blockRow = transaction.Select<byte[], Block>("Block", key);
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
                dbreezeTransaction.RemoveKey<byte[]>("Transaction", transaction.GetHash().ToBytes());
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
                dbreezeTransaction.RemoveKey<byte[]>("Block", block.GetHash().ToBytes());
            }

            this.logger.LogTrace("(-)");
        }

        private List<Block> GetBlocksFromHashes(DBreeze.Transactions.Transaction dbreezeTransaction, List<uint256> hashes)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(hashes), nameof(hashes.Count), hashes?.Count);

            var blocks = new List<Block>();

            foreach (uint256 hash in hashes)
            {
                byte[] key = hash.ToBytes();

                Row<byte[], Block> blockRow = dbreezeTransaction.Select<byte[], Block>("Block", key);
                if (blockRow.Exists)
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                    blocks.Add(blockRow.Value);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(blocks.Count), blocks.Count);
            return blocks;
        }

        public Task DeleteAsync(uint256 newBlockHash, List<uint256> hashes)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(newBlockHash), newBlockHash, nameof(hashes), nameof(hashes.Count), hashes?.Count);
            Guard.NotNull(newBlockHash, nameof(newBlockHash));
            Guard.NotNull(hashes, nameof(hashes));

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("Block", "Common", "Transaction");
                    transaction.ValuesLazyLoadingIsOn = false;

                    List<Block> blocks = this.GetBlocksFromHashes(transaction, hashes);
                    this.OnDeleteBlocks(transaction, blocks);
                    this.SaveBlockHash(transaction, newBlockHash);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        public DBreezeEngine GetDbreezeEngine()
        {
            return this.DBreeze;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DBreeze.Dispose();
        }
    }
}