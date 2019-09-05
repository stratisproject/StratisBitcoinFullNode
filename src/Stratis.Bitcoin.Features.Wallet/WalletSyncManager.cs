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
using Stratis.Features.SQLiteWalletRepository;

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
        private readonly IWalletRepository walletRepository;
        private List<string> wallets = new List<string>();

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private IAsyncLoop walletSynchronisationLoop;
        private SubscriptionToken blockConnectedSubscription;
        private SubscriptionToken transactionReceivedSubscription;
        private ConcurrentDictionary<string, WalletSyncState> walletStateMap = new ConcurrentDictionary<string, WalletSyncState>();

        protected ChainedHeader walletTip;

        public ChainedHeader WalletTip => this.walletTip;

        public bool ContainsWallets => this.wallets.Any();

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ChainIndexer chainIndexer,
            Network network, IBlockStore blockStore, StoreSettings storeSettings, ISignals signals, IAsyncProvider asyncProvider, IWalletRepository walletRepository, INodeLifetime nodeLifetime)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chainIndexer, nameof(chainIndexer));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockStore, nameof(blockStore));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));
            Guard.NotNull(walletRepository, nameof(walletRepository));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.walletManager = walletManager;
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.storeSettings = storeSettings;
            this.signals = signals;
            this.asyncProvider = asyncProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletRepository = walletRepository;
            this.nodeLifetime = nodeLifetime;
        }

        /// <inheritdoc />
        public void Start()
        {
            // ToDo check if this is still required
            if (this.storeSettings.PruningEnabled) throw new WalletException("Wallet can not yet run on a pruned node");

            // ToDo get rid of call to wallet manager, wallet manager has to go!
            this.logger.LogInformation("WalletSyncManager starting. Wallet at block {0}.", this.walletManager.LastBlockHeight());

            // Start sync job for wallets
            this.walletSynchronisationLoop = this.asyncProvider.CreateAndRunAsyncLoop("WalletSyncManager.OrchestrateWalletSync",
                token =>
                {
                    this.OrchestrateWalletSync(); 
                    return Task.CompletedTask;
                }, 
                this.nodeLifetime.ApplicationStopping, 
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));

            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(this.OnTransactionAvailable);
        }

        private void OnTransactionAvailable(TransactionReceived transactionReceived)
        {
            this.ProcessTransaction(transactionReceived.ReceivedTransaction);
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            this.ProcessBlock(blockConnected.ConnectedBlock.Block);
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.signals.Unsubscribe(this.blockConnectedSubscription);
            this.signals.Unsubscribe(this.transactionReceivedSubscription);
        }

        /// <inheritdoc />
        public virtual void ProcessBlock(Block block)
        {
            // ToDo Fshutdown to come up with how to call this
            // in conjunction with Sync Job
        }

        /// <inheritdoc />
        public virtual void ProcessTransaction(Transaction transaction)
        {
            if (!this.ContainsWallets) return;

            Guard.NotNull(transaction, nameof(transaction));
            
            this.wallets.ForEach(wallet => this.walletRepository.ProcessTransaction(wallet, transaction));
        }

        /// <inheritdoc />
        public virtual void SyncFromDate(DateTime date)
        {
            throw new Exception("Not Implemented!");
        }

        /// <inheritdoc />
        public virtual void SyncFromHeight(int height)
        {
            throw new Exception("Not Implemented!");
        }

        private void OrchestrateWalletSync()
        {
            this.wallets = ((SQLiteWalletRepository)this.walletRepository).GetWalletNames();

            if (this.ContainsWallets)
            {
                foreach (string wallet in this.wallets)
                {
                    try
                    {
                        this.walletTip = this.walletRepository.FindFork(wallet, this.chainIndexer.Tip);
                        bool walletIsNotSyncing = this.walletStateMap.TryAdd(wallet, WalletSyncState.Syncing);

                        if (walletIsNotSyncing)
                        {
                            this.walletRepository.RewindWallet(wallet, this.walletTip);
                            ProccessRangeToRepo(this.walletTip.Height + 1, this.chainIndexer.Tip.Height, wallet);
                            this.walletStateMap.TryRemove(wallet, out _);
                        }
                    }
                    catch (Exception ex)
                    {
                        //ToDo handle exception here
                        this.walletStateMap.TryRemove(wallet, out _);
                        //this needs to be elaborated how it will work during reorg
                        this.logger.LogInformation("Error calling find fork");
                    }
                }
            }
        }

        private void ProccessRangeToRepo(int leftBoundry, int rightBoundry, string wallet)
        {
            IEnumerable<(ChainedHeader, Block)> range =
                this.BatchBlocksFromRange(leftBoundry, rightBoundry, wallet);

            this.walletRepository.ProcessBlocks(range, wallet);
        }

        private IEnumerable<(ChainedHeader, Block)> BatchBlocksFromRange(int leftBoundry, int rightBoundry, string wallet)
        {
            //ToDo this bit need proper review
            // This is were optimisation needs to happen
            // It is possible it will be more efficient to have this to add
            // all hashes to a list and then call List<Block> blocks = this.BlockRepo.GetBlocks(hashes);
            for (int x = leftBoundry; x <= rightBoundry; x++)
            {
                // This might cause issues so I think there should be some other way to monitor 
                // height
                this.walletTip = this.walletRepository.FindFork(wallet, this.chainIndexer.Tip);
                ChainedHeader chainedHeader = this.chainIndexer.GetHeader(x);
                Block block = this.blockStore.GetBlock(chainedHeader.HashBlock);
                yield return (chainedHeader, block);
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
    
    /// <summary>
    /// This should be further developed for monitoring state per each wallet
    /// </summary>
    public enum WalletSyncState
    {
        Idle = 0,
        Syncing = 1,
        Completed = 2
    }
}
