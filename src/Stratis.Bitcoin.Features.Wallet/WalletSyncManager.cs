using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
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
        private SubscriptionToken transactionReceivedSubscription;
        private CancellationTokenSource syncCancellationToken;
        private object lockObject;

        public ChainedHeader WalletTip => this.walletManager.WalletCommonTip(this.chainIndexer.Tip);

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ChainIndexer chainIndexer,
            Network network, IBlockStore blockStore, StoreSettings storeSettings, ISignals signals, IAsyncProvider asyncProvider, INodeLifetime nodeLifetime)
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

            this.logger.LogInformation("WalletSyncManager starting.");

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

            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(this.OnTransactionAvailable);
        }

        private void OnTransactionAvailable(TransactionReceived transactionReceived)
        {
            this.ProcessTransaction(transactionReceived.ReceivedTransaction);
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.syncCancellationToken.Cancel();
            this.walletSynchronisationLoop?.Dispose();
            this.signals.Unsubscribe(this.transactionReceivedSubscription);
        }

        /// <inheritdoc />
        public virtual void ProcessBlock(Block block)
        {
            lock (this.lockObject)
            {
                this.walletManager.ProcessBlock(block);
            }
        }

        /// <inheritdoc />
        public virtual void ProcessTransaction(Transaction transaction)
        {
            lock (this.lockObject)
            {
                this.walletManager.ProcessTransaction(transaction);
            }
        }

        /// <inheritdoc />
        public void ProcessBlocks()
        {
            lock (this.lockObject)
            {
                this.walletManager.ProcessBlocks((height) => { return this.BatchBlocksFromRange(height, this.chainIndexer.Tip.Height); });
            }
        }

        /// <inheritdoc />
        public virtual void SyncFromDate(DateTime date)
        {
            lock (this.lockObject)
            {
                int syncFromHeight = this.chainIndexer.GetHeightAtTime(date);

                this.SyncFromHeight(syncFromHeight);
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
            catch (Exception)
            {
                // TODO: Log the error but keep going.
            }
        }

        public IEnumerable<(ChainedHeader, Block)> BatchBlocksFromRange(int leftBoundry, int rightBoundry)
        {
            for (int height = leftBoundry; height <= rightBoundry && !this.syncCancellationToken.IsCancellationRequested;)
            {
                var hashes = new List<uint256>();
                for (int i = 0; i < 100 && (height + i) <= rightBoundry; i++)
                {
                    ChainedHeader header = this.chainIndexer.GetHeader(height + i);
                    hashes.Add(header.HashBlock);
                }

                long flagFall = DateTime.Now.Ticks;

                List<Block> blocks = this.blockStore.GetBlocks(hashes);

                var buffer = new List<(ChainedHeader, Block)>();
                for (int i = 0; i < 100 && height <= rightBoundry && !this.syncCancellationToken.IsCancellationRequested; height++, i++)
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
