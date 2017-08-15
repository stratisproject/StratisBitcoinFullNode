using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.BlockStore;
using System.Linq;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public interface IIndexRepository : Stratis.Bitcoin.Features.BlockStore.IBlockRepository
    {
        Task<bool> CreateIndex(string name, bool multiValue, string builder, string[] dependencies = null);
        Task<bool> DropIndex(string name);
        Task<KeyValuePair<string, Index>[]> ListIndexes(Func<KeyValuePair<string, Index>, bool> include = null);

        Task<byte[]> Lookup(string indexName, byte[] key);
        Task<List<byte[]>> LookupMany(string indexName, byte[] key);
        Task<List<byte[]>> Lookup(string indexName, List<byte[]> keys);
    }

    public class IndexSession:DBreezeSingleThreadSession
    {
        public IndexSession(string threadName, string folder):
            base(threadName, folder)
        {            
        }

        public List<string> GetIndexTables()
        {
            return this.engine.Scheme.GetUserTableNamesStartingWith(IndexRepository.indexTablePrefix);
        }

        public void DeleteTable(string name)
        {
            this.engine.Scheme.DeleteTable(name);
        }
    }

    public class IndexRepository : BlockRepository, IIndexRepository
    {
        public Dictionary<string, Index> Indexes;
        //public new BlockStoreRepositoryPerformanceCounter PerformanceCounter { get; }

        public static string indexTablePrefix { get { return "Index_"; } }

        public string IndexTableName(string indexName)
        {
            return indexTablePrefix + indexName;
        }

        public IndexRepository(Network network, DataFolder dataFolder)
            : this(network, dataFolder.IndexPath)
        {
        }

        public IndexRepository(Network network, string folder):
            base(network, new IndexSession("DBreeze IndexRepository", folder))
        {
            this.tableNames = new HashSet<string>() { "Block", "Transaction", "Common" };
            this.Indexes = new Dictionary<string, Index>();
 
            this.session.Execute(() =>
            {
                // Discover and add indexes to dictionary and tables to syncronize
                foreach (var row in this.session.Transaction.SelectForwardStartsWith<string, string>("Common", indexTablePrefix))
                {
                    if (!row.Exists) continue;
                    var name = row.Key.Substring(indexTablePrefix.Length);
                    var index = Index.Parse(this, row.Value, row.Key);
                    if (index.compiled != null)
                    {
                        this.Indexes.Add(name, index);
                        if (!this.tableNames.Contains(row.Key))
                            this.tableNames.Add(row.Key);
                    }
                }

                // Remove any index tables that are not being used (not transactional)
                foreach (string indexTable in (this.session as IndexSession).GetIndexTables())
                    if (!this.session.Transaction.Select<string, string>("Common", indexTable).Exists)
                        (this.session as IndexSession).DeleteTable(indexTable);
            }).GetAwaiter().GetResult();
        }

        public override BlockStoreRepositoryPerformanceCounter PerformanceCounterFactory()
        {
            return new IndexStoreRepositoryPerformanceCounter();
        }

        public override Task Initialize()
        {
            base.Initialize().GetAwaiter().GetResult();

            this.session.Execute(() =>
            {
                // Ensure this is set so that the base code calls us to index blocks
                SetTxIndex(true);

                // Clean-up any invalid indexes
                foreach (var row in this.session.Transaction.SelectForwardStartsWith<string, string>("Common", indexTablePrefix))
                {
                    var name = row.Key.Substring(indexTablePrefix.Length);
                    if (!this.Indexes.ContainsKey(name))
                        DropIndex(name, this.session.Transaction);
                }

                // One commit per execute
                this.session.Transaction.Commit();
            }).GetAwaiter().GetResult();

            return Task.CompletedTask;
        }

        public Task<bool> DropIndex(string name)
        {
            Guard.NotNull(name, nameof(name));

            return this.session.Execute(() =>
            {
                DropIndex(name, this.session.Transaction);

                this.session.Transaction.Commit();

                return true;
            });
        }

        private void DropIndex(string name, DBreeze.Transactions.Transaction transaction)
        {
            var indexTableName = IndexTableName(name);

            if (transaction.Select<string, string>("Common", indexTableName).Exists)
            {
                transaction.RemoveAllKeys(indexTableName, true);
                transaction.RemoveKey<string>("Common", indexTableName);
                if (this.tableNames.Contains(indexTableName))
                    this.tableNames.Remove(indexTableName);
            }
        }

        public Task<KeyValuePair<string, Index>[]> ListIndexes(Func<KeyValuePair<string, Index>, bool> include = null)
        {
            return this.session.Execute(() =>
            {
                return this.Indexes.Where(include).ToArray();
            });
        }

        public Task<bool> CreateIndex(string name, bool multiValue, string builder, string[] dependencies = null)
        {
            Guard.NotNull(name, nameof(name));

            return this.session.Execute(() =>
            {
                if (this.Indexes.ContainsKey(name))
                    throw new IndexStoreException("The '" + name + "' index already exists");

                var index = new Index(this, name, multiValue, builder, dependencies);
                this.Indexes[name] = index;

                var dbIndex = this.session.Transaction.Select<string, string>("Common", index.Table);
                if (dbIndex.Exists)
                    this.session.Transaction.RemoveAllKeys(index.Table, true);

                if (!this.tableNames.Contains(index.Table))
                    this.tableNames.Add(index.Table);

                var transactions = new List<(Transaction, Block)>();
                foreach (var row in this.session.Transaction.SelectForward<byte[], Block>("Block"))
                {
                    var block = row.Value;
                    var key = block.GetHash().ToBytes();

                    foreach (var transaction in block.Transactions)
                        transactions.Add((transaction, block));

                    if (transactions.Count >= 5000)
                    {
                        index.IndexTransactionDetails(transactions);
                        transactions.Clear();
                    }
                }

                index.IndexTransactionDetails(transactions);
                this.session.Transaction.Insert<string, string>("Common", index.Table, index.ToString());
                this.session.Transaction.Commit();

                return true;
            });
        }        

        public IndexSession GetSession()
        {
            return this.session as IndexSession;
        }

        public Network GetNetwork()
        {
            return this.network;
        }

        public Task<byte[]> Lookup(string indexName, byte[] key)
        {
            Guard.NotNull(indexName, nameof(indexName));
            Guard.NotNull(key, nameof(key));
            Guard.Assert(this.Indexes.TryGetValue(indexName, out Index index));
            Guard.Assert(!index.Many);

            return this.session.Execute(() =>
            {
                return index.LookupMultiple(new List<byte[]> { key }).ToList()[0];
            });
        }

        public Task<List<byte[]>> LookupMany(string indexName, byte[] key)
        {
            Guard.NotNull(indexName, nameof(indexName));
            Guard.NotNull(key, nameof(key));
            Guard.Assert(this.Indexes.TryGetValue(indexName, out Index index));
            Guard.Assert(index.Many);

            return this.session.Execute(() =>
            {
                return index.EnumerateValues(key).ToList();
            });
        }

        public Task<List<byte[]>> Lookup(string indexName, List<byte[]> keys)
        {
            Guard.NotNull(indexName, nameof(indexName));
            Guard.NotNull(keys, nameof(keys));
            Guard.Assert(this.Indexes.TryGetValue(indexName, out Index index));
            Guard.Assert(!index.Many);

            return this.session.Execute(() =>
            {
                return index.LookupMultiple(keys).ToList();
            });
        }

        protected override void OnInsertTransactions(List<(Transaction, Block)> transactions)
        {
            base.OnInsertTransactions(transactions);
            foreach (var index in this.Indexes.Values)
                index.IndexTransactionDetails(transactions);
        }
        
        protected override void OnDeleteTransactions(List<(Transaction, Block)> transactions)
        {
            foreach (var index in this.Indexes.Values)
                index.IndexTransactionDetails(transactions, true);
            base.OnDeleteTransactions(transactions);
        }       
    }
}
