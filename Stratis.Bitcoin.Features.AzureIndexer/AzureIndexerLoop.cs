using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Auth;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.AzureIndexer.IndexTasks;
using Stratis.Bitcoin.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.AzureIndexer
{
    /// <summary>
    /// The BlockStoreLoop simultaneously finds and downloads blocks and stores them in the BlockRepository.
    /// </summary>
    public class AzureIndexerLoop
    {
        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoopChain;

        public FullNode FullNode { get; }
        //private readonly BlockStoreStats blockStoreStats;

        /// <summary> Best chain of block headers.</summary>
        internal readonly ConcurrentChain Chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        private readonly INodeLifetime nodeLifetime;

        private const int IndexBatchSize = 100;

        /// <summary>The minimum amount of blocks that can be stored in Pending Storage before they get processed.</summary>
        public const int PendingStorageBatchThreshold = 10;

        /// <summary>The chain of steps that gets executed to find and download blocks.</summary>
        //private BlockStoreStepChain stepChain;

        public virtual string StoreName { get { return "AzureIndexer"; } }

        private readonly AzureIndexerSettings indexerSettings;

        /// <summary>The highest stored block in the repository.</summary>
        internal ChainedBlock StoreTip { get; private set; }

        /// <summary>The connection manager.</summary>
        protected readonly IConnectionManager connectionManager;

        public AzureIndexer AzureIndexer { get; private set; }
        public IndexerConfiguration IndexerConfig { get; private set; }

        /// <summary>Public constructor for unit testing</summary>
        public AzureIndexerLoop(IAsyncLoopFactory asyncLoopFactory,
            FullNode fullNode,
            ConcurrentChain chain,
            ConnectionManager connectionManager,
            ChainState chainState,
            AzureIndexerSettings indexerSettings,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.FullNode = fullNode;
            this.Chain = chain;
            this.connectionManager = connectionManager;
            this.nodeLifetime = nodeLifetime;
            this.indexerSettings = indexerSettings;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.loggerFactory = loggerFactory;
        }

        public void Initialize()
        {
            this.logger.LogTrace("()");

            IndexerConfiguration indexerConfig = new IndexerConfiguration();

            indexerConfig.StorageCredentials = new StorageCredentials(
                this.indexerSettings.AzureAccountName, this.indexerSettings.AzureKey);
            indexerConfig.StorageNamespace = "";
            indexerConfig.Network = this.connectionManager.Network;
            indexerConfig.CheckpointSetName = this.indexerSettings.CheckpointsetName;
            indexerConfig.AzureStorageEmulatorUsed = this.indexerSettings.AzureEmulatorUsed;

            this.IndexerConfig = indexerConfig;

            var indexer = indexerConfig.CreateIndexer();
            indexer.Configuration.EnsureSetup();
            indexer.TaskScheduler = new CustomThreadPoolTaskScheduler(30, 100);
            indexer.CheckpointInterval = this.indexerSettings.CheckpointInterval;
            indexer.IgnoreCheckpoints = this.indexerSettings.IgnoreCheckpoints;
            indexer.FromHeight = this.indexerSettings.From;
            indexer.ToHeight = this.indexerSettings.To;

            this.AzureIndexer = indexer;

            if (this.indexerSettings.IgnoreCheckpoints)
                this.SetStoreTip(this.Chain.GetBlock(this.indexerSettings.From));
            else
            {
                int minHeight = int.MaxValue;
                minHeight = Math.Min(minHeight, this.GetCheckPointBlock(IndexerCheckpoints.Blocks).Height);
                minHeight = Math.Min(minHeight, this.GetCheckPointBlock(IndexerCheckpoints.Balances).Height);
                minHeight = Math.Min(minHeight, this.GetCheckPointBlock(IndexerCheckpoints.Transactions).Height);
                minHeight = Math.Min(minHeight, this.GetCheckPointBlock(IndexerCheckpoints.Wallets).Height);

                this.SetStoreTip(this.Chain.GetBlock(minHeight));
            }

            this.StartLoop();
            
            this.logger.LogTrace("(-)");
        }

        private ChainedBlock GetCheckPointBlock(IndexerCheckpoints indexerCheckpoints)
        {
            Checkpoint checkpoint = this.AzureIndexer.GetCheckpointInternal(indexerCheckpoints);
            return this.Chain.FindFork(checkpoint.BlockLocator);
        }

        /// <summary>
        /// Executes the indexing loops.
        /// </summary>
        private void StartLoop()
        {
            this.asyncLoop = this.asyncLoopFactory.Run($"{this.StoreName}.IndexAsync", async token =>
            {
                await IndexAsync(this.nodeLifetime.ApplicationStopping);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.RunOnce,
            startAfter: TimeSpans.FiveSeconds);

            this.asyncLoopChain = this.asyncLoopFactory.Run($"{this.StoreName}.IndexChainAsync", async token =>
            {
                await IndexChainAsync(this.nodeLifetime.ApplicationStopping);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.RunOnce,
            startAfter: TimeSpans.Minute);

        }

        /// <summary>
        /// Shuts the indexing loops down.
        /// </summary>
        public void Shutdown()
        {
            this.asyncLoop.Dispose();
            this.asyncLoopChain.Dispose();
        }

        private BlockFetcher GetBlockFetcher(IndexerCheckpoints indexerCheckpoints, CancellationToken cancellationToken)
        {
            Checkpoint checkpoint = this.AzureIndexer.GetCheckpointInternal(indexerCheckpoints);
            FullNodeBlocksRepository repo = new FullNodeBlocksRepository(this.FullNode);
            return new BlockFetcher(checkpoint, repo, this.Chain)
            {
                NeedSaveInterval = this.indexerSettings.CheckpointInterval,
                FromHeight = this.StoreTip.Height + 1,
                ToHeight = Math.Min(this.StoreTip.Height + IndexBatchSize, this.indexerSettings.To),
                CancellationToken = cancellationToken
            };
        }

        /// <summary>
        /// Performs Indexing into Azure Storage.
        /// <para>
        private async Task IndexChainAsync(CancellationToken cancellationToken)
        {
            this.logger.LogTrace("()");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {                  
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        this.AzureIndexer.IndexChain(this.Chain, cancellationToken);
                    }
                }
                catch (Exception)
                {
                    // Try again 1 minute later
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ContinueWith(t => { }).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Performs Indexing into Azure Storage.
        /// <para>
        private async Task IndexAsync(CancellationToken cancellationToken)
        {
            this.logger.LogTrace("()");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // All indexes will progress more or less in step
                    // Use 'minHeight' to track the current indexed height
                    int minHeight = int.MaxValue;

                    // Index a batch of blocks
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var task = new IndexBlocksTask(this.IndexerConfig);
                        task.SaveProgression = !this.indexerSettings.IgnoreCheckpoints;
                        var fetcher = this.GetBlockFetcher(IndexerCheckpoints.Blocks, cancellationToken);
                        task.Index(fetcher, this.AzureIndexer.TaskScheduler);
                        minHeight = Math.Min(minHeight, fetcher._LastProcessed.Height);
                    }

                    // Index a batch of transactions
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var task = new IndexTransactionsTask(this.IndexerConfig);
                        task.SaveProgression = !this.indexerSettings.IgnoreCheckpoints;
                        var fetcher = this.GetBlockFetcher(IndexerCheckpoints.Transactions, cancellationToken);
                        task.Index(fetcher, this.AzureIndexer.TaskScheduler);
                        minHeight = Math.Min(minHeight, fetcher._LastProcessed.Height);
                    }

                    // Index a batch of balances
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var task = new IndexBalanceTask(this.IndexerConfig, null);
                        task.SaveProgression = !this.indexerSettings.IgnoreCheckpoints;
                        var fetcher = this.GetBlockFetcher(IndexerCheckpoints.Balances, cancellationToken);
                        task.Index(fetcher, this.AzureIndexer.TaskScheduler);
                        minHeight = Math.Min(minHeight, fetcher._LastProcessed.Height);
                    }

                    // Index a batch of wallets
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var task = new IndexBalanceTask(this.IndexerConfig, this.IndexerConfig.CreateIndexerClient().GetAllWalletRules());
                        task.SaveProgression = !this.indexerSettings.IgnoreCheckpoints;
                        var fetcher = this.GetBlockFetcher(IndexerCheckpoints.Wallets, cancellationToken);
                        task.Index(fetcher, this.AzureIndexer.TaskScheduler);
                        minHeight = Math.Min(minHeight, fetcher._LastProcessed.Height);
                    }

                    // Update the StoreTip value from the minHeight
                    this.SetStoreTip(this.Chain.GetBlock(Math.Min(minHeight, this.indexerSettings.To)));
                }
                catch (Exception ex)
                {
                    // If something goes wrong then try again 1 minute later
                    IndexerTrace.ErrorWhileImportingBlockToAzure(this.StoreTip.HashBlock, ex);
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ContinueWith(t => { }).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the store's tip</summary>
        internal void SetStoreTip(ChainedBlock chainedBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedBlock), chainedBlock?.HashBlock);
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.StoreTip = chainedBlock;

            this.logger.LogTrace("(-)");
        }
    }
}