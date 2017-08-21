using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockRepository : IDisposable
    {
        Task Initialize();
        Task PutAsync(uint256 nextBlockHash, List<Block> blocks);
        Task<Block> GetAsync(uint256 hash);
        Task<Transaction> GetTrxAsync(uint256 trxid);
        Task DeleteAsync(uint256 newlockHash, List<uint256> hashes);
        Task<bool> ExistAsync(uint256 hash);
        Task<uint256> GetTrxBlockIdAsync(uint256 trxid);
        Task SetBlockHash(uint256 nextBlockHash);
        Task SetTxIndex(bool txIndex);
        uint256 BlockHash { get; }
        BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }
        bool TxIndex { get; }
    }

    public class BlockRepository : IBlockRepository
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        protected readonly DBreezeSingleThreadSession session;
        protected readonly Network network;

        protected static readonly byte[] BlockHashKey = new byte[0];
        protected HashSet<string> tableNames = new HashSet<string>() { "Block", "Transaction", "Common" };
        protected static readonly byte[] TxIndexKey = new byte[1];

        public uint256 BlockHash { get; private set; }
        public BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }
        public bool TxIndex { get; private set; }

        public BlockRepository(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory)
            : this(network, dataFolder.BlockPath, loggerFactory)
        {
        }

        public BlockRepository(Network network, string folder, ILoggerFactory loggerFactory)
            : this(network, (new DBreezeSingleThreadSession($"DBreeze BlockRepository", folder)), loggerFactory)
        {
        }

        public BlockRepository(Network network, DBreezeSingleThreadSession session, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(session, nameof(session));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.session = session;
            this.network = network;
            this.PerformanceCounter = PerformanceCounterFactory();
        }

        public virtual BlockStoreRepositoryPerformanceCounter PerformanceCounterFactory()
        {
            return new BlockStoreRepositoryPerformanceCounter();
        }

        public virtual Task Initialize()
        {
            this.logger.LogTrace("()");
            var genesis = this.network.GetGenesis();

            var sync = this.session.Execute(() =>
            {
                this.session.Transaction.SynchronizeTables(this.tableNames.ToList());
                this.session.Transaction.ValuesLazyLoadingIsOn = true;
            });

            var hash = this.session.Execute(() =>
            {
                if (this.LoadBlockHash() == null)
                {
                    this.SaveBlockHash(genesis.GetHash());
                    this.session.Transaction.Commit();
                }
                if (this.LoadTxIndex() == null)
                {
                    this.SaveTxIndex(false);
                    this.session.Transaction.Commit();
                }
            });

            Task res = Task.WhenAll(new[] { sync, hash });
            this.logger.LogTrace("(-)");
            return res;
        }

        public bool LazyLoadingOn
        {
            get { return this.session.Transaction.ValuesLazyLoadingIsOn; }
            set { this.session.Transaction.ValuesLazyLoadingIsOn = value; }
        }

        public Task<Transaction> GetTrxAsync(uint256 trxid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
                return Task.FromResult(default(Transaction));

            Task<Transaction> res = this.session.Execute(() =>
            {
                this.logger.LogTrace("()");
                var blockid = this.session.Transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());
                if (!blockid.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    this.logger.LogTrace("(-)[NO_BLOCK]:null");
                    return null;
                }

                this.PerformanceCounter.AddRepositoryHitCount(1);
                var block = this.session.Transaction.Select<byte[], Block>("Block", blockid.Value.ToBytes());
                var trx = block?.Value?.Transactions.FirstOrDefault(t => t.GetHash() == trxid);

                if (trx == null)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }

                this.logger.LogTrace("(-):{0}", trx);
                return trx;
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        public Task<uint256> GetTrxBlockIdAsync(uint256 trxid)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(trxid), trxid);
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
            {
                this.logger.LogTrace("(-)[NO_TXINDEX]");
                return Task.FromResult(default(uint256));
            }

            Task<uint256> res = this.session.Execute(() =>
            {
                this.logger.LogTrace("()");
                var blockid = this.session.Transaction.Select<byte[], uint256>("Transaction", trxid.ToBytes());

                if (!blockid.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    this.logger.LogTrace("(-):null");
                    return null;
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                    this.logger.LogTrace("(-):'{0}'", blockid.Value);
                    return blockid.Value;
                }
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        virtual protected void OnInsertBlocks(List<Block> blocks)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blocks), nameof(blocks.Count), blocks?.Count);
            var transactions = new List<(Transaction, Block)>();

            var byteListComparer = new ByteListComparer();

            var blockDict = new Dictionary<uint256, Block>();

            // Gather blocks
            foreach (var block in blocks)
            {
                var blockId = block.GetHash();
                blockDict[blockId] = block;
            }

            // Sort blocks. Be consistent in always converting our keys to byte arrays using the ToBytes method.
            var blockList = blockDict.ToList();
            blockList.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Key.ToBytes(), pair2.Key.ToBytes()));

            // Index blocks
            foreach (KeyValuePair<uint256, Block> kv in blockList)
            {
                var blockId = kv.Key;
                var block = kv.Value;

                // if the block is already in store don't write it again
                var item = this.session.Transaction.Select<byte[], Block>("Block", blockId.ToBytes());
                if (!item.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                    this.PerformanceCounter.AddRepositoryInsertCount(1);
                    this.session.Transaction.Insert<byte[], Block>("Block", blockId.ToBytes(), block);

                    if (this.TxIndex)
                        foreach (var transaction in block.Transactions)
                            transactions.Add((transaction, block));
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }
            }

            if (this.TxIndex)
                this.OnInsertTransactions(transactions);

            this.logger.LogTrace("(-)");
        }

        virtual protected void OnInsertTransactions(List<(Transaction, Block)> transactions)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(transactions), nameof(transactions.Count), transactions?.Count);

            var byteListComparer = new ByteListComparer();

            transactions.Sort((pair1, pair2) => byteListComparer.Compare(pair1.Item1.GetHash().ToBytes(), pair2.Item1.GetHash().ToBytes()));

            // Index transactions
            foreach (var (transaction, block) in transactions)
            {
                this.PerformanceCounter.AddRepositoryInsertCount(1);
                this.session.Transaction.Insert<byte[], uint256>("Transaction", transaction.GetHash().ToBytes(), block.GetHash());
            }

            this.logger.LogTrace("(-)");
        }

        public Task PutAsync(uint256 nextBlockHash, List<Block> blocks)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(nextBlockHash), nextBlockHash, nameof(blocks), nameof(blocks.Count), blocks?.Count);
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
            Guard.NotNull(blocks, nameof(blocks));

            // dbreeze is faster if sort ascending by key in memory before insert
            // however we need to find how byte arrays are sorted in dbreeze this link can help 
            // https://docs.google.com/document/pub?id=1IFkXoX3Tc2zHNAQN9EmGSXZGbabMrWmpmVxFsLxLsw

            Task res = this.session.Execute(() =>
            {
                this.OnInsertBlocks(blocks);

                // Commit additions
                this.SaveBlockHash(nextBlockHash);
                this.session.Transaction.Commit();
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        private bool? LoadTxIndex()
        {
            this.logger.LogTrace("()");

            var item = this.session.Transaction.Select<byte[], bool>("Common", TxIndexKey);

            if (!item.Exists)
            {
                this.PerformanceCounter.AddRepositoryMissCount(1);

                this.logger.LogTrace("(-):null");
                return null;
            }
            else
            {
                this.PerformanceCounter.AddRepositoryHitCount(1);
                this.TxIndex = item.Value;

                this.logger.LogTrace("(-):{0}", item.Value);
                return item.Value;
            }
        }

        private void SaveTxIndex(bool txIndex)
        {
            this.logger.LogTrace("({0}:{1})", nameof(txIndex), txIndex);

            this.TxIndex = txIndex;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
            this.session.Transaction.Insert<byte[], bool>("Common", TxIndexKey, txIndex);

            this.logger.LogTrace("(-)");
        }

        public Task SetTxIndex(bool txIndex)
        {
            this.logger.LogTrace("({0}:{1})", nameof(txIndex), txIndex);

            Task res = this.session.Execute(() =>
            {
                this.SaveTxIndex(txIndex);
                this.session.Transaction.Commit();
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        private uint256 LoadBlockHash()
        {
            this.logger.LogTrace("()");

            this.BlockHash = this.BlockHash ?? this.session.Transaction.Select<byte[], uint256>("Common", BlockHashKey)?.Value;

            this.logger.LogTrace("(-):'{0}'", this.BlockHash);
            return this.BlockHash;
        }

        public Task SetBlockHash(uint256 nextBlockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(nextBlockHash), nextBlockHash);
            Guard.NotNull(nextBlockHash, nameof(nextBlockHash));

            Task res = this.session.Execute(() =>
            {
                this.SaveBlockHash(nextBlockHash);
                this.session.Transaction.Commit();
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        private void SaveBlockHash(uint256 nextBlockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(nextBlockHash), nextBlockHash);

            this.BlockHash = nextBlockHash;
            this.PerformanceCounter.AddRepositoryInsertCount(1);
            this.session.Transaction.Insert<byte[], uint256>("Common", BlockHashKey, nextBlockHash);

            this.logger.LogTrace("(-)");
        }

        public Task<Block> GetAsync(uint256 hash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(hash), hash);
            Guard.NotNull(hash, nameof(hash));

            Task<Block> res = this.session.Execute(() =>
            {
                this.logger.LogTrace("()");

                var key = hash.ToBytes();
                var item = this.session.Transaction.Select<byte[], Block>("Block", key);
                if (!item.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }

                this.logger.LogTrace("(-):{0}", item?.Value);
                return item?.Value;
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        public Task<bool> ExistAsync(uint256 hash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(hash), hash);
            Guard.NotNull(hash, nameof(hash));

            Task<bool> res = this.session.Execute(() =>
            {
                this.logger.LogTrace("()");

                var key = hash.ToBytes();
                var item = this.session.Transaction.Select<byte[], Block>("Block", key);
                if (!item.Exists)
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                }

                this.logger.LogTrace("(-):{0}", item.Exists);
                return item.Exists; // lazy loading is on so we don't fetch the whole value, just the row.
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        protected virtual void OnDeleteTransactions(List<(Transaction, Block)> transactions)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(transactions), nameof(transactions.Count), transactions?.Count);

            foreach (var transaction in transactions)
            {
                this.PerformanceCounter.AddRepositoryDeleteCount(1);
                this.session.Transaction.RemoveKey<byte[]>("Transaction", transaction.Item1.GetHash().ToBytes());
            }

            this.logger.LogTrace("(-)");
        }

        protected virtual void OnDeleteBlocks(List<Block> blocks)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(blocks), nameof(blocks.Count), blocks?.Count);

            if (this.TxIndex)
            {
                var transactions = new List<(Transaction, Block)>();

                foreach (var block in blocks)
                    foreach (var transaction in block.Transactions)
                        transactions.Add((transaction, block));

                this.OnDeleteTransactions(transactions);
            }

            foreach (var block in blocks)
            {
                this.PerformanceCounter.AddRepositoryDeleteCount(1);
                this.session.Transaction.RemoveKey<byte[]>("Block", block.GetHash().ToBytes());
            }

            this.logger.LogTrace("(-)");
        }

        private List<Block> GetBlocksFromHashes(List<uint256> hashes)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(hashes), nameof(hashes.Count), hashes?.Count);

            var blocks = new List<Block>();

            foreach (var hash in hashes)
            {
                var key = hash.ToBytes();

                var block = this.session.Transaction.Select<byte[], Block>("Block", key);
                if (block.Exists)
                {
                    this.PerformanceCounter.AddRepositoryHitCount(1);
                    blocks.Add(block.Value);
                }
                else
                {
                    this.PerformanceCounter.AddRepositoryMissCount(1);
                }
            }

            this.logger.LogTrace("(-):*.{0}={1}", nameof(blocks.Count), blocks?.Count);
            return blocks;
        }

        public Task DeleteAsync(uint256 newlockHash, List<uint256> hashes)
        {
            this.logger.LogTrace("({0}:'{1}',{2}.{3}:{4})", nameof(newlockHash), newlockHash, nameof(hashes), nameof(hashes.Count), hashes?.Count);
            Guard.NotNull(newlockHash, nameof(newlockHash));
            Guard.NotNull(hashes, nameof(hashes));

            Task res = this.session.Execute(() =>
            {
                this.logger.LogTrace("()");

                this.OnDeleteBlocks(this.GetBlocksFromHashes(hashes));
                this.SaveBlockHash(newlockHash);
                this.session.Transaction.Commit();

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return res;
        }

        public void Dispose()
        {
            this.session.Dispose();
        }
    }
}