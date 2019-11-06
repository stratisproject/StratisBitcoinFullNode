using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletSyncManager : IWalletSyncManager, IDisposable
    {
        private readonly IWalletManager walletManager;
        private readonly ChainIndexer chainIndexer;
        private readonly ILogger logger;
        private readonly IBlockStore blockStore;
        private readonly StoreSettings storeSettings;
        private readonly ISignals signals;
        private readonly IAsyncProvider asyncProvider;
        private List<(string name, ChainedHeader tipHeader)> wallets = new List<(string name, ChainedHeader tipHeader)>();
        private readonly INodeLifetime nodeLifetime;
        private IAsyncLoop walletSynchronisationLoop;
        private SubscriptionToken transactionAddedSubscription;
        private SubscriptionToken transactionRemovedSubscription;
        private SubscriptionToken blockConnectedSubscription;
        private CancellationTokenSource syncCancellationToken;
        private object lockObject;
        private readonly MempoolManager mempoolManager;

        public ChainedHeader WalletTip => this.walletManager.WalletCommonTip(this.chainIndexer.Tip);

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ChainIndexer chainIndexer,
            Network network, IBlockStore blockStore, StoreSettings storeSettings, ISignals signals, IAsyncProvider asyncProvider, INodeLifetime nodeLifetime,
            MempoolManager mempoolManager = null)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockStore, nameof(blockStore));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.mempoolManager = mempoolManager;
            this.walletManager = walletManager;
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.storeSettings = storeSettings;
            this.signals = signals;
            this.asyncProvider = asyncProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.syncCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping);
            this.lockObject = new object();
        }

        /// <inheritdoc />
        public void Start()
        {
            // ToDo check if this is still required
            if (this.storeSettings.PruningEnabled)
            {
                throw new WalletException("Wallet can not yet run on a pruned node");
            }

            this.logger.LogInformation("WalletSyncManager synchronising with mempool.");

            // Ensure that all mempool transactions that apply to wallets have been applied.
            if (this.mempoolManager != null)
            {
                foreach (uint256 trxId in this.mempoolManager.GetMempoolAsync().GetAwaiter().GetResult())
                {
                    Transaction transaction = this.mempoolManager.GetTransaction(trxId).GetAwaiter().GetResult();
                    this.walletManager.ProcessTransaction(transaction);
                }
            }

            this.logger.LogInformation("WalletSyncManager starting synchronisation loop.");

            // Start sync job for wallets
            this.walletSynchronisationLoop = this.asyncProvider.CreateAndRunAsyncLoop("WalletSyncManager.OrchestrateWalletSync",
                token =>
                {
                    this.OrchestrateWalletSync();
                    return Task.CompletedTask;
                },
                this.syncCancellationToken.Token,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));

            this.transactionAddedSubscription = this.signals.Subscribe<TransactionAddedToMemoryPool>(this.OnTransactionAdded);
            this.transactionRemovedSubscription = this.signals.Subscribe<TransactionRemovedFromMemoryPool>(this.OnTransactionRemoved);
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
        }

        private void OnTransactionAdded(TransactionAddedToMemoryPool transactionAddedToMempool)
        {
            this.logger.LogDebug("Adding transaction '{0}' as it was added to the mempool.", transactionAddedToMempool.AddedTransaction.GetHash());
            this.walletManager.ProcessTransaction(transactionAddedToMempool.AddedTransaction);
        }

        private void OnTransactionRemoved(TransactionRemovedFromMemoryPool transactionRemovedFromMempool)
        {
            this.logger.LogDebug("Transaction '{0}' was removed from the mempool. RemovedForBlock={1}", 
                transactionRemovedFromMempool.RemovedTransaction.GetHash(), transactionRemovedFromMempool.RemovedForBlock);

            if (!transactionRemovedFromMempool.RemovedForBlock)
            {
                this.walletManager.RemoveUnconfirmedTransaction(transactionRemovedFromMempool.RemovedTransaction);
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.syncCancellationToken.Cancel();
            this.walletSynchronisationLoop?.RunningTask.GetAwaiter().GetResult();
            this.signals.Unsubscribe(this.transactionAddedSubscription);
            this.signals.Unsubscribe(this.transactionRemovedSubscription);

            this.logger.LogInformation("WalletSyncManager stopped.");
        }

        /// <inheritdoc />
        public virtual void ProcessBlock(Block block)
        {
        }

        /// <inheritdoc />
        public virtual void ProcessTransaction(Transaction transaction)
        {
            this.walletManager.ProcessTransaction(transaction);
        }

        private void ProcessBlocks()
        {
            this.walletManager.ProcessBlocks((height) => { return this.BatchBlocksFrom(height); });
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            this.ProcessBlock(blockConnected.ConnectedBlock.Block);
        }

        /// <inheritdoc />
        public virtual void SyncFromDate(DateTime date, string walletName = null)
        {
            lock (this.lockObject)
            {
                int syncFromHeight = this.chainIndexer.GetHeightAtTime(date);

                this.SyncFromHeight(syncFromHeight, walletName);
            }
        }


        /// <inheritdoc />
        public virtual void SyncFromHeight(int height, string walletName = null)
        {
            lock (this.lockObject)
            {
                if (height > (this.chainIndexer.Tip?.Height ?? -1))
                    throw new WalletException("Can't start sync beyond end of chain");

                if (walletName != null)
                    this.walletManager.RewindWallet(walletName, this.chainIndexer.GetHeader(height - 1));
                else
                    foreach (string wallet in this.walletManager.GetWalletsNames())
                        this.walletManager.RewindWallet(wallet, this.chainIndexer.GetHeader(height - 1));
            }
        }

        internal void OrchestrateWalletSync()
        {
            try
            {
                this.ProcessBlocks();
            }
            catch (Exception e)
            {
                // Log the error but keep going.
                this.logger.LogError("'{0}' failed with: {1}.", nameof(OrchestrateWalletSync), e.ToString());
            }
        }

        private IEnumerable<(ChainedHeader, Block)> BatchBlocksFrom(int leftBoundry)
        {
            for (int height = leftBoundry; !this.syncCancellationToken.IsCancellationRequested;)
            {
                var hashes = new List<uint256>();
                for (int i = 0; i < 100; i++)
                {
                    ChainedHeader header = this.chainIndexer.GetHeader(height + i);
                    if (header == null)
                        break;

                    hashes.Add(header.HashBlock);
                }

                if (hashes.Count == 0)
                    yield break;

                long flagFall = DateTime.Now.Ticks;

                List<Block> blocks = this.blockStore.GetBlocks(hashes);

                var buffer = new List<(ChainedHeader, Block)>();
                for (int i = 0; i < blocks.Count && !this.syncCancellationToken.IsCancellationRequested; height++, i++)
                {
                    ChainedHeader header = this.chainIndexer.GetHeader(height);
                    yield return ((header, blocks[i]));
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool dispose)
        {
            if (dispose)
            {
                this.Stop();
            }
        }
    }
}
