using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// A lock for managing asynchronous access to memory pool.
    /// </summary>
    public class MempoolSchedulerLock : SchedulerLock
    {
    }

    /// <summary>
    /// The memory pool manager contains high level methods that can be used from outside of the mempool.
    /// Includes querying information about the transactions in the memory pool.
    /// Also includes methods for persisting memory pool.
    /// </summary>
    public class MempoolManager : IPooledTransaction, IPooledGetUnspentTransaction
    {
        /// <summary>Memory pool persistence methods for loading and saving from storage.</summary>
        private IMempoolPersistence mempoolPersistence;

        /// <summary>Instance logger for memory pool manager.</summary>
        private readonly ILogger logger;

        /// <summary>Transaction memory pool for managing transactions in the memory pool.</summary>
        private readonly ITxMempool memPool;

        /// <summary>Coin view of the memory pool.</summary>
        private readonly ICoinView coinView;

        private readonly Network network;

        public MempoolManager(
            MempoolSchedulerLock mempoolLock,
            ITxMempool memPool,
            IMempoolValidator validator,
            IDateTimeProvider dateTimeProvider,
            MempoolSettings mempoolSettings,
            IMempoolPersistence mempoolPersistence,
            ICoinView coinView,
            ILoggerFactory loggerFactory,
            Network network)
        {
            this.MempoolLock = mempoolLock;
            this.memPool = memPool;
            this.DateTimeProvider = dateTimeProvider;
            this.mempoolSettings = mempoolSettings;
            this.Validator = validator;
            this.mempoolPersistence = mempoolPersistence;
            this.coinView = coinView;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Lock for memory pool access.</summary>
        public MempoolSchedulerLock MempoolLock { get; }

        /// <summary>Memory pool validator for validating transactions.</summary>
        public IMempoolValidator Validator { get; }

        /// <summary>Date and time information provider.</summary>
        public IDateTimeProvider DateTimeProvider { get; }

        /// <summary>Settings for memory pool.</summary>
        public MempoolSettings mempoolSettings { get; set; }

        /// <summary>Access to memory pool validator performance counter.</summary>
        public MempoolPerformanceCounter PerformanceCounter => this.Validator.PerformanceCounter;

        /// <inheritdoc />
        public async Task<Transaction> GetTransaction(uint256 trxid)
        {
            return (await this.InfoAsync(trxid))?.Trx;
        }

        /// <summary>
        /// Gets the memory pool transactions.
        /// </summary>
        /// <returns>List of transactions</returns>
        public Task<List<uint256>> GetMempoolAsync()
        {
            return this.MempoolLock.ReadAsync(() => this.memPool.MapTx.Keys.ToList());
        }

        /// <summary>
        /// Gets a list of transaction information from the memory pool.
        /// </summary>
        /// <returns>List of transaction information.</returns>
        public List<TxMempoolInfo> InfoAll()
        {
            // TODO: DepthAndScoreComparator

            var infoList = this.memPool.MapTx.DescendantScore.Select(item => new TxMempoolInfo
            {
                Trx = item.Transaction,
                Time = item.Time,
                FeeRate = new FeeRate(item.Fee, (int)item.GetTxSize()),
                FeeDelta = item.ModifiedFee - item.Fee
            }).ToList();

            return infoList;
        }

        /// <summary>
        /// Check whether a transaction exists in the mempool.
        /// </summary>
        public Task<bool> ExistsAsync(uint256 trxid)
        {
            return this.MempoolLock.ReadAsync(() => this.memPool.Exists(trxid));
        }

        /// <summary>
        /// Loads the memory pool asynchronously from a file.
        /// </summary>
        /// <param name="fileName">Filename to load from.</param>
        internal async Task LoadPoolAsync(string fileName = null)
        {
            if (this.mempoolPersistence != null && this.memPool?.MapTx != null && this.Validator != null)
            {
                this.logger.LogInformation("Loading Memory Pool.");
                IEnumerable<MempoolPersistenceEntry> entries = this.mempoolPersistence.Load(this.network, fileName);
                await this.AddMempoolEntriesToMempoolAsync(entries);
            }
            else
            {
                this.logger.LogInformation("Unable to load memory pool cache from '{0}'.", fileName);
            }
        }

        /// <summary>
        /// Saves the memory pool to persistent storage.
        /// </summary>
        /// <returns>Memory pool save result.</returns>
        internal MemPoolSaveResult SavePool()
        {
            if (this.mempoolPersistence == null)
            {
                this.logger.LogTrace("(-)[NON_SUCCESS]");
                return MemPoolSaveResult.NonSuccess;
            }
            MemPoolSaveResult saveResult = this.mempoolPersistence.Save(this.network, this.memPool);

            return saveResult;
        }

        /// <summary>
        /// Gets transaction information for a specific transaction.
        /// </summary>
        /// <param name="hash">Hash of the transaction to query.</param>
        /// <returns>Transaction information.</returns>
        public TxMempoolInfo Info(uint256 hash)
        {
            TxMempoolEntry item = this.memPool.MapTx.TryGet(hash);
            var infoItem = item == null ? null : new TxMempoolInfo
            {
                Trx = item.Transaction,
                Time = item.Time,
                FeeRate = new FeeRate(item.Fee, (int)item.GetTxSize()),
                FeeDelta = item.ModifiedFee - item.Fee
            };

            return infoItem;
        }

        /// <summary>
        /// Gets transaction information for all transactions in memory pool.
        /// </summary>
        /// <returns>List of transaction information.</returns>
        public Task<List<TxMempoolInfo>> InfoAllAsync()
        {
            return this.MempoolLock.ReadAsync(this.InfoAll);
        }

        /// <summary>
        /// Gets transaction info for a specific transaction in memory pool.
        /// </summary>
        /// <param name="hash">Hash of the transaction to query.</param>
        /// <returns>Transaction information.</returns>
        public Task<TxMempoolInfo> InfoAsync(uint256 hash)
        {
            return this.MempoolLock.ReadAsync(() => this.Info(hash));
        }

        /// <summary>
        /// Gets the memory pool size.
        /// </summary>
        /// <returns>Memory pool size.</returns>
        public Task<long> MempoolSize()
        {
            return this.MempoolLock.ReadAsync(() => this.memPool.Size);
        }

        /// <summary>
        /// Clears the memory pool.
        /// </summary>
        public Task Clear()
        {
            return this.MempoolLock.ReadAsync(() => this.memPool.Clear());
        }

        /// <summary>
        /// Gets the memory pool dynamic memory usage.
        /// </summary>
        /// <returns>Dynamic memory usage.</returns>
        public Task<long> MempoolDynamicMemoryUsage()
        {
            return this.MempoolLock.ReadAsync(() => this.memPool.DynamicMemoryUsage());
        }

        /// <inheritdoc />
        public async Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            TxMempoolInfo txInfo = await this.InfoAsync(trxid);
            if (txInfo == null)
            {
                this.logger.LogTrace("(-):[TX_IS_NULL]");
                return null;
            }

            var memPoolCoinView = new MempoolCoinView(this.coinView, this.memPool, this.MempoolLock, this.Validator);
            await memPoolCoinView.LoadViewAsync(txInfo.Trx);
            UnspentOutputs unspentOutputs = memPoolCoinView.GetCoins(trxid);
            
            return unspentOutputs;
        }

        /// <summary>
        /// Add persisted mempool entries to the memory pool.
        /// </summary>
        /// <param name="entries">Entries read from mempool cache.</param>
        internal async Task AddMempoolEntriesToMempoolAsync(IEnumerable<MempoolPersistenceEntry> entries)
        {
            int i = 0;
            if (entries != null)
            {
                // tx timeout in seconds
                long expiryTimeout = this.mempoolSettings.MempoolExpiry * 60 * 60;

                this.logger.LogInformation("Loaded {0} cached entries.", entries.Count());
                foreach (MempoolPersistenceEntry entry in entries)
                {
                    Transaction trx = entry.Tx;
                    uint256 trxHash = trx.GetHash();
                    long currentTime = this.DateTimeProvider.GetTime();

                    if ((entry.Time + expiryTimeout) <= currentTime)
                    {
                        this.logger.LogDebug("Transaction ID '{0}' not accepted to mempool due to age of {1:0.##} days.", trxHash, TimeSpan.FromSeconds(this.DateTimeProvider.GetTime() - entry.Time).TotalDays);
                        continue;
                    }

                    if (this.memPool.Exists(trxHash))
                    {
                        this.logger.LogDebug("Transaction ID '{0}' not accepted to mempool because it already exists.", trxHash);
                        continue;
                    }
                    var state = new MempoolValidationState(false) { AcceptTime = entry.Time, OverrideMempoolLimit = true };
                    if (await this.Validator.AcceptToMemoryPoolWithTime(state, trx) && this.memPool.MapTx.ContainsKey(trxHash))
                    {
                        this.logger.LogDebug("Transaction ID '{0}' accepted to mempool.", trxHash);
                        i++;
                        this.memPool.MapTx[trxHash].UpdateFeeDelta(entry.FeeDelta);
                    }
                    else
                    {
                        this.logger.LogDebug("Transaction ID '{0}' not accepted to mempool because its invalid.", trxHash);
                    }
                }
                this.logger.LogInformation("{0} entries accepted.", i);
            }
        }
    }
}
