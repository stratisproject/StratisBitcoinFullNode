using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexRepository : Stratis.Bitcoin.Features.BlockStore.IBlockRepository
    {
        Task<bool> CreateIndexAsync(string name, bool multiValue, string builder, string[] dependencies = null);
        Task<bool> DropIndexAsync(string name);
        KeyValuePair<string, Index>[] ListIndexes(Func<KeyValuePair<string, Index>, bool> include = null);

        Task<byte[]> LookupAsync(string indexName, byte[] key);
        Task<List<byte[]>> LookupManyAsync(string indexName, byte[] key);
        Task<List<byte[]>> LookupAsync(string indexName, List<byte[]> keys);
    }

    public class IndexRepository : BlockRepository, IIndexRepository
    {
        private readonly HashSet<string> tableNames;
        public ConcurrentDictionary<string, Index> Indexes;
        private Dictionary<string, IndexExpression> requiredIndexes;

        private const string IndexTablePrefix = "Index_";

        public string IndexTableName(string indexName)
        {
            return IndexTablePrefix + indexName;
        }

        public IndexRepository(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, IndexSettings indexSettings = null)
            : this(network, dataFolder.IndexPath, dateTimeProvider, loggerFactory, indexSettings.indexes)
        {
        }

        public IndexRepository(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, Dictionary<string, IndexExpression> requiredIndexes = null):
            base(network, folder, dateTimeProvider, loggerFactory)
        {
            this.tableNames = new HashSet<string> { "Block", "Transaction", "Common" };
            this.Indexes = new ConcurrentDictionary<string, Index>();
            this.requiredIndexes = requiredIndexes;

            using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
            {
                transaction.SynchronizeTables("Block", "Common");
                transaction.ValuesLazyLoadingIsOn = false;

                // Discover and add indexes to dictionary and tables to synchronize.
                foreach (Row<string, string> row in transaction.SelectForwardStartsWith<string, string>("Common", IndexTablePrefix))
                {
                    if (!row.Exists) continue;

                    string name = row.Key.Substring(IndexTablePrefix.Length);
                    Index index = Index.Parse(this, row.Value, row.Key);
                    if (index.compiled != null)
                    {
                        this.Indexes.AddOrReplace(name, index);
                        if (!this.tableNames.Contains(row.Key))
                            this.tableNames.Add(row.Key);
                    }
                }

                // Remove any index tables that are not being used (not transactional).
                foreach (string indexTable in this.GetIndexTables())
                    if (!transaction.Select<string, string>("Common", indexTable).Exists)
                        this.DeleteTable(indexTable);
            }
        }

        public override BlockStoreRepositoryPerformanceCounter PerformanceCounterFactory()
        {
            return new IndexStoreRepositoryPerformanceCounter(this.dateTimeProvider);
        }

        public override Task InitializeAsync()
        {
            Task task = base.InitializeAsync().ContinueWith((o) => 
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("Block", "Common");

                    // Ensure this is set so that the base code calls us to index blocks.
                    SaveTxIndex(transaction, true);

                    // Clean-up any invalid indexes.
                    foreach (Row<string, string> row in transaction.SelectForwardStartsWith<string, string>("Common", IndexTablePrefix))
                    {
                        string name = row.Key.Substring(IndexTablePrefix.Length);
                        if (!this.Indexes.ContainsKey(name))
                            DropIndex(name, transaction);
                    }

                    // Create configured indexes that do not exist yet.
                    transaction.ValuesLazyLoadingIsOn = false;
                    if (this.requiredIndexes != null)
                    {
                        foreach (KeyValuePair<string, IndexExpression> kv in this.requiredIndexes)
                        {
                            if (!this.Indexes.ContainsKey(kv.Key))
                            {
                                IndexExpression index = kv.Value;
                                this.CreateIndex(transaction, kv.Key, index.Many, index.Builder, index.Dependencies);
                            }
                        }
                    }

                    // One commit per transaction.
                    transaction.Commit();
                }
            });

            return task; 
        }

        public Task<bool> DropIndexAsync(string name)
        {
            Guard.NotNull(name, nameof(name));

            Task<bool> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    DropIndex(name, transaction);
                    transaction.Commit();
                }
                return true;
            });

            return task;
        }

        private void DropIndex(string name, DBreeze.Transactions.Transaction transaction)
        {
            string indexTableName = IndexTableName(name);

            if (transaction.Select<string, string>("Common", indexTableName).Exists)
            {
                transaction.RemoveAllKeys(indexTableName, true);
                transaction.RemoveKey<string>("Common", indexTableName);
                if (this.tableNames.Contains(indexTableName))
                    this.tableNames.Remove(indexTableName);
            }
        }

        public KeyValuePair<string, Index>[] ListIndexes(Func<KeyValuePair<string, Index>, bool> include = null)
        {
            return this.Indexes.Where(include).ToArray();
        }

        public Task<bool> CreateIndexAsync(string name, bool multiValue, string builder, string[] dependencies = null)
        {
            Guard.NotNull(name, nameof(name));
            Guard.NotNull(builder, nameof(builder));

            Task<bool> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    this.CreateIndex(transaction, name, multiValue, builder, dependencies);
                    transaction.Commit();
                }
                return true;
            });

            return task;
        }

        private void CreateIndex(DBreeze.Transactions.Transaction dbreezeTransaction, string name, bool multiValue, string builder, string[] dependencies)
        {
            if (this.Indexes.ContainsKey(name))
                throw new IndexStoreException("The '" + name + "' index already exists");

            var index = new Index(this, name, multiValue, builder, dependencies);
            this.Indexes[name] = index;

            Row<string, string> dbIndexRow = dbreezeTransaction.Select<string, string>("Common", index.Table);
            if (dbIndexRow.Exists)
                dbreezeTransaction.RemoveAllKeys(index.Table, true);

            if (!this.tableNames.Contains(index.Table))
                this.tableNames.Add(index.Table);

            var transactions = new List<(Transaction, Block)>();
            foreach (Row<byte[], Block> row in dbreezeTransaction.SelectForward<byte[], Block>("Block"))
            {
                Block block = row.Value;
                byte[] key = block.GetHash().ToBytes();

                foreach (Transaction transaction in block.Transactions)
                    transactions.Add((transaction, block));

                if (transactions.Count >= 5000)
                {
                    index.IndexTransactionDetails(dbreezeTransaction, transactions);
                    transactions.Clear();
                }
            }

            index.IndexTransactionDetails(dbreezeTransaction, transactions);
            dbreezeTransaction.Insert<string, string>("Common", index.Table, index.ToString());
        }

        public Network GetNetwork()
        {
            return this.network;
        }

        public Task<byte[]> LookupAsync(string indexName, byte[] key)
        {
            Guard.NotNull(indexName, nameof(indexName));
            Guard.NotNull(key, nameof(key));

            Index index;
            bool indexExists = this.Indexes.TryGetValue(indexName, out index);

            Guard.Assert(indexExists);
            Guard.Assert(!index.Many);

            Task<byte[]> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    return index.LookupMultiple(transaction, new List<byte[]> { key }).ToList()[0];
                }
            });

            return task;
        }

        public Task<List<byte[]>> LookupManyAsync(string indexName, byte[] key)
        {
            Guard.NotNull(indexName, nameof(indexName));
            Guard.NotNull(key, nameof(key));

            Index index;
            bool indexExists = this.Indexes.TryGetValue(indexName, out index);

            Guard.Assert(indexExists);
            Guard.Assert(index.Many);

            Task<List<byte[]>> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    return index.EnumerateValues(transaction, key).ToList();
                }
            });

            return task;
        }

        public Task<List<byte[]>> LookupAsync(string indexName, List<byte[]> keys)
        {
            Guard.NotNull(indexName, nameof(indexName));
            Guard.NotNull(keys, nameof(keys));

            Index index;
            bool indexExists = this.Indexes.TryGetValue(indexName, out index);

            Guard.Assert(indexExists);
            Guard.Assert(!index.Many);

            Task<List<byte[]>> task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.DBreeze.GetTransaction())
                {
                    return index.LookupMultiple(transaction, keys).ToList();
                }
            });

            return task;
        }

        protected override void OnInsertTransactions(DBreeze.Transactions.Transaction dbreezeTransaction, List<(Transaction, Block)> transactions)
        {
            base.OnInsertTransactions(dbreezeTransaction, transactions);

            foreach (Index index in this.Indexes.Values)
                index.IndexTransactionDetails(dbreezeTransaction, transactions);
        }

        protected override void OnDeleteTransactions(DBreeze.Transactions.Transaction dbreezeTransaction, List<(Transaction, Block)> transactions)
        {
            foreach (Index index in this.Indexes.Values)
                index.IndexTransactionDetails(dbreezeTransaction, transactions, true);

            base.OnDeleteTransactions(dbreezeTransaction, transactions);
        }

        public List<string> GetIndexTables()
        {
            return this.DBreeze.Scheme.GetUserTableNamesStartingWith(IndexTablePrefix);
        }

        public void DeleteTable(string name)
        {
            this.DBreeze.Scheme.DeleteTable(name);
        }
    }
}
