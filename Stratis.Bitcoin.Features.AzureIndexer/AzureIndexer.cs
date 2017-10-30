using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Indexer.Converters;
using NBitcoin.Indexer.IndexTasks;
using NBitcoin.Indexer.Internal;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public enum IndexerCheckpoints
    {
        Wallets,
        Transactions,
        Blocks,
        Balances
    }
    public class AzureIndexer
    {
        public static AzureIndexer CreateIndexer(IConfiguration config)
        {
            var indexerConfig = IndexerConfiguration.FromConfiguration(config);
            return indexerConfig.CreateIndexer();
        }


        private readonly IndexerConfiguration _Configuration;
        public IndexerConfiguration Configuration
        {
            get
            {
                return _Configuration;
            }
        }
        public AzureIndexer(IndexerConfiguration configuration)
        {
            if(configuration == null)
                throw new ArgumentNullException("configuration");
            TaskScheduler = TaskScheduler.Default;
            CheckpointInterval = TimeSpan.FromMinutes(15.0);
            _Configuration = configuration;
            FromHeight = 0;
            ToHeight = 99999999;
        }

        public long IndexTransactions(ChainBase chain = null)
        {
            using(IndexerTrace.NewCorrelation("Import transactions to azure started"))
            {
                using(var node = Configuration.ConnectToNode(false))
                {

                    node.VersionHandshake();

                    var task = new IndexTransactionsTask(Configuration);
                    task.SaveProgression = !IgnoreCheckpoints;
                    task.Index(GetBlockFetcher(GetCheckpointInternal(IndexerCheckpoints.Transactions), node, chain), TaskScheduler);
                    return task.IndexedEntities;
                }
            }
        }

        private Checkpoint GetCheckpointInternal(IndexerCheckpoints checkpoint)
        {
            var chk = GetCheckpoint(checkpoint);
            if(IgnoreCheckpoints)
                chk = new Checkpoint(chk.CheckpointName, Configuration.Network, null, null);
            return chk;
        }

        private void SetThrottling()
        {
            Helper.SetThrottling();
            ServicePoint tableServicePoint = ServicePointManager.FindServicePoint(Configuration.CreateTableClient().BaseUri);
            tableServicePoint.ConnectionLimit = 1000;
        }

        private void PushTransactions(MultiValueDictionary<string, TransactionEntry.Entity> buckets,
                                        IEnumerable<TransactionEntry.Entity> indexedTransactions,
                                    BlockingCollection<TransactionEntry.Entity[]> transactions)
        {
            var array = indexedTransactions.ToArray();
            transactions.Add(array);
            buckets.Remove(array[0].PartitionKey);
        }

        TimeSpan _Timeout = TimeSpan.FromMinutes(5.0);

        /// <summary>
        /// TaskScheduler to parallelize individual object Index methods
        /// </summary>
        public TaskScheduler TaskScheduler
        {
            get;
            set;
        }

        public void Index(params Block[] blocks)
        {
            var task = new IndexBlocksTask(Configuration);
            task.Index(blocks, TaskScheduler);
        }
        public Task IndexAsync(params Block[] blocks)
        {
            var task = new IndexBlocksTask(Configuration);
            return task.IndexAsync(blocks, TaskScheduler);
        }
        public void Index(params TransactionEntry.Entity[] entities)
        {
            Index(entities.Select(e => e.CreateTableEntity()).ToArray(), Configuration.GetTransactionTable());
        }
        public Task IndexAsync(params TransactionEntry.Entity[] entities)
        {
            return IndexAsync(entities.Select(e => e.CreateTableEntity()).ToArray(), Configuration.GetTransactionTable());
        }

        public void Index(IEnumerable<OrderedBalanceChange> balances)
        {
            Index(balances.Select(b => b.ToEntity()), Configuration.GetBalanceTable());
        }
        public Task IndexAsync(IEnumerable<OrderedBalanceChange> balances)
        {
            return IndexAsync(balances.Select(b => b.ToEntity()), Configuration.GetBalanceTable());
        }
        private void Index(IEnumerable<ITableEntity> entities, CloudTable table)
        {
            var task = new IndexTableEntitiesTask(Configuration, table);
            task.Index(entities, TaskScheduler);
        }
        private Task IndexAsync(IEnumerable<ITableEntity> entities, CloudTable table)
        {
            var task = new IndexTableEntitiesTask(Configuration, table);
            return task.IndexAsync(entities, TaskScheduler);
        }        
        
        public long IndexBlocks(ChainBase chain = null)
        {
            using(IndexerTrace.NewCorrelation("Import blocks to azure started"))
            {
                using(var node = Configuration.ConnectToNode(false))
                {
                    node.VersionHandshake();
                    var task = new IndexBlocksTask(Configuration);
                    task.SaveProgression = !IgnoreCheckpoints;
                    task.Index(GetBlockFetcher(GetCheckpointInternal(IndexerCheckpoints.Blocks), node, chain), TaskScheduler);
                    return task.IndexedBlocks;
                }
            }
        }

        public Checkpoint GetCheckpoint(IndexerCheckpoints checkpoint)
        {
            return GetCheckpointRepository().GetCheckpoint(checkpoint.ToString().ToLowerInvariant());
        }
        public Task<Checkpoint> GetCheckpointAsync(IndexerCheckpoints checkpoint)
        {
            return GetCheckpointRepository().GetCheckpointAsync(checkpoint.ToString().ToLowerInvariant());
        }

        public CheckpointRepository GetCheckpointRepository()
        {
            return new CheckpointRepository(_Configuration.GetBlocksContainer(), _Configuration.Network, string.IsNullOrWhiteSpace(_Configuration.CheckpointSetName) ? "default" : _Configuration.CheckpointSetName);
        }


        /// <summary>
        /// Get a block fetcher of the specified chain from the specified checkpoint
        /// </summary>
        /// <param name="checkpoint">The checkpoint to load from</param>
        /// <param name="chain">The chain to fetcher (default: the Node's main chain)</param>
        /// <returns>A BlockFetcher for enumerating blocks and saving progression</returns>
        public BlockFetcher GetBlockFetcher(Checkpoint checkpoint, Node node, ChainBase chain = null)
        {
            if(checkpoint == null)
                throw new ArgumentNullException("checkpoint");
            if(node == null)
                throw new ArgumentNullException("node");
            chain = chain ?? GetNodeChain(node);
            IndexerTrace.CheckpointLoaded(chain.FindFork(checkpoint.BlockLocator), checkpoint.CheckpointName);
            return new BlockFetcher(checkpoint, new NodeBlocksRepository(node), chain)
            {
                NeedSaveInterval = CheckpointInterval,
                FromHeight = FromHeight,
                ToHeight = ToHeight
            };
        }

        /// <summary>
        /// Get a block fetcher of the specified chain from the specified checkpoint
        /// </summary>
        /// <param name="checkpoint">The checkpoint name to load from</param>
        /// <param name="chain">The chain to fetcher (default: the Node's main chain)</param>
        /// <returns>A BlockFetcher for enumerating blocks and saving progression</returns>
        public BlockFetcher GetBlockFetcher(string checkpointName, Node node, ChainBase chain = null)
        {
            if(checkpointName == null)
                throw new ArgumentNullException("checkpointName");
            return GetBlockFetcher(GetCheckpointRepository().GetCheckpoint(checkpointName), node, chain);
        }

        public int IndexOrderedBalances(ChainBase chain)
        {
            using(IndexerTrace.NewCorrelation("Import balances to azure started"))
            {
                using(var node = Configuration.ConnectToNode(false))
                {
                    node.VersionHandshake();
                    var task = new IndexBalanceTask(Configuration, null);
                    task.SaveProgression = !IgnoreCheckpoints;
                    task.Index(GetBlockFetcher(GetCheckpointInternal(IndexerCheckpoints.Balances), node, chain), TaskScheduler);
                    return task.IndexedEntities;
                }
            }
        }

        internal ChainBase GetMainChain()
        {
            return Configuration.CreateIndexerClient().GetMainChain();
        }

        public int IndexWalletBalances(ChainBase chain)
        {
            using(IndexerTrace.NewCorrelation("Import wallet balances to azure started"))
            {
                using(var node = Configuration.ConnectToNode(false))
                {
                    node.VersionHandshake();
                    var task = new IndexBalanceTask(Configuration, Configuration.CreateIndexerClient().GetAllWalletRules());
                    task.SaveProgression = !IgnoreCheckpoints;
                    task.Index(GetBlockFetcher(GetCheckpointInternal(IndexerCheckpoints.Wallets), node, chain), TaskScheduler);
                    return task.IndexedEntities;
                }
            }
        }

        public void IndexOrderedBalance(int height, Block block)
        {
            var table = Configuration.GetBalanceTable();
            var blockId = block == null ? null : block.GetHash();
            var header = block == null ? null : block.Header;

            var entities =
                    block
                        .Transactions
                        .SelectMany(t => OrderedBalanceChange.ExtractScriptBalances(t.GetHash(), t, blockId, header, height))
                        .Select(_ => _.ToEntity())
                        .AsEnumerable();

            Index(entities, table);
        }

        public void IndexTransactions(int height, Block block)
        {
            var table = Configuration.GetTransactionTable();
            var blockId = block == null ? null : block.GetHash();
            var entities =
                        block
                        .Transactions
                        .Select(t => new TransactionEntry.Entity(t.GetHash(), t, blockId))
                        .Select(c => c.CreateTableEntity())
                        .AsEnumerable();
            Index(entities, table);
        }

        public void IndexWalletOrderedBalance(int height, Block block, WalletRuleEntryCollection walletRules)
        {
            try
            {
                IndexWalletOrderedBalanceAsync(height, block, walletRules).Wait();
            }
            catch(AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
        public Task IndexWalletOrderedBalanceAsync(int height, Block block, WalletRuleEntryCollection walletRules)
        {
            var table = Configuration.GetBalanceTable();
            var blockId = block == null ? null : block.GetHash();

            var entities =
                    block
                    .Transactions
                    .SelectMany(t => OrderedBalanceChange.ExtractWalletBalances(null, t, blockId, block.Header, height, walletRules))
                    .Select(t => t.ToEntity())
                    .AsEnumerable();

            return IndexAsync(entities, table);
        }

        public void IndexOrderedBalance(Transaction tx)
        {
            var table = Configuration.GetBalanceTable();
            var entities = OrderedBalanceChange.ExtractScriptBalances(tx).Select(t => t.ToEntity()).AsEnumerable();
            Index(entities, table);
        }
        public Task IndexOrderedBalanceAsync(Transaction tx)
        {
            var table = Configuration.GetBalanceTable();
            var entities = OrderedBalanceChange.ExtractScriptBalances(tx).Select(t => t.ToEntity()).AsEnumerable();
            return IndexAsync(entities, table);
        }

        public ChainBase GetNodeChain()
        {
            IndexerTrace.Information("Connecting to node " + Configuration.Node);
            using(var node = Configuration.ConnectToNode(false))
            {
                IndexerTrace.Information("Handshaking");
                node.VersionHandshake();
                return GetNodeChain(node);
            }
        }

        public ChainBase GetNodeChain(Node node)
        {
            var chain = new ConcurrentChain(Configuration.Network);
            IndexerTrace.Information("Synchronizing with local node");
            node.SynchronizeChain(chain);
            IndexerTrace.Information("Chain loaded with height " + chain.Height);
            return chain;
        }
        public void IndexNodeMainChain()
        {
            var chain = GetNodeChain();
            IndexChain(chain);
        }


        internal const int BlockHeaderPerRow = 6;
        internal void Index(ChainBase chain, int startHeight)
        {
            List<ChainPartEntry> entries = new List<ChainPartEntry>(((chain.Height - startHeight) / BlockHeaderPerRow) + 5);
            startHeight = startHeight - (startHeight % BlockHeaderPerRow);
            ChainPartEntry chainPart = null;
            for(int i = startHeight; i <= chain.Tip.Height; i++)
            {
                if(chainPart == null)
                    chainPart = new ChainPartEntry()
                    {
                        ChainOffset = i
                    };

                var block = chain.GetBlock(i);
                chainPart.BlockHeaders.Add(block.Header);
                if(chainPart.BlockHeaders.Count == BlockHeaderPerRow)
                {
                    entries.Add(chainPart);
                    chainPart = null;
                }
            }
            if(chainPart != null)
                entries.Add(chainPart);
            Index(entries);
        }

        private void Index(List<ChainPartEntry> chainParts)
        {
            CloudTable table = Configuration.GetChainTable();
            TableBatchOperation batch = new TableBatchOperation();
            var last = chainParts[chainParts.Count - 1];
            foreach(var entry in chainParts)
            {
                batch.Add(TableOperation.InsertOrReplace(entry.ToEntity()));
                if(batch.Count == 100)
                {
                    table.ExecuteBatchAsync(batch).GetAwaiter().GetResult();
                    batch = new TableBatchOperation();
                }
                IndexerTrace.RemainingBlockChain(entry.ChainOffset, last.ChainOffset + last.BlockHeaders.Count - 1);
            }
            if(batch.Count > 0)
            {
                table.ExecuteBatchAsync(batch).GetAwaiter().GetResult();
            }
        }


        public TimeSpan CheckpointInterval
        {
            get;
            set;
        }

        public int FromHeight
        {
            get;
            set;
        }

        public bool IgnoreCheckpoints
        {
            get;
            set;
        }

        public void IndexChain(ChainBase chain)
        {
            if(chain == null)
                throw new ArgumentNullException("chain");
            SetThrottling();

            using(IndexerTrace.NewCorrelation("Index main chain to azure started"))
            {
                Configuration.GetChainTable().CreateIfNotExistsAsync().GetAwaiter().GetResult();
                IndexerTrace.InputChainTip(chain.Tip);
                var client = Configuration.CreateIndexerClient();
                var changes = client.GetChainChangesUntilFork(chain.Tip, true).ToList();

                var height = 0;
                if(changes.Count != 0)
                {
                    IndexerTrace.IndexedChainTip(changes[0].BlockId, changes[0].Height);
                    if(changes[0].Height > chain.Tip.Height)
                    {
                        IndexerTrace.InputChainIsLate();
                        return;
                    }
                    height = changes[changes.Count - 1].Height + 1;
                    if(height > chain.Height)
                    {
                        IndexerTrace.IndexedChainIsUpToDate(chain.Tip);
                        return;
                    }
                }
                else
                {
                    IndexerTrace.NoForkFoundWithStored();
                }

                IndexerTrace.IndexingChain(chain.GetBlock(height), chain.Tip);
                Index(chain, height);

            }
        }

        public int ToHeight
        {
            get;
            set;
        }
    }
}
