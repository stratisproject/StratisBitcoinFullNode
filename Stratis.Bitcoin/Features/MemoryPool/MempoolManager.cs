using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Memory pool scheduler.
    /// </summary>
    public class MempoolScheduler : AsyncLock
    { }

    /// <summary>
    /// Memory pool manager.
    /// </summary>
    public class MempoolManager
    {
        #region Fields

        /// <summary>
        /// Memory pool persistence injected dependency.
        /// </summary>
        private IMempoolPersistence mempoolPersistence;

        /// <summary>
        /// Memory pool manager logger.
        /// </summary>
        private readonly ILogger mempoolLogger;

        /// <summary>
        /// Transaction memory pool injected dependency.
        /// </summary>
        private readonly TxMempool memPool;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of a memory pool manager object.
        /// </summary>
        /// <param name="mempoolScheduler">Memory pool scheduler injected dependency.</param>
        /// <param name="memPool">Transaction memory pool injected dependency.</param>
        /// <param name="validator">Memory pool validator injected dependency.</param>
        /// <param name="orphans">Memory pool orphans injected dependency.</param>
        /// <param name="dateTimeProvider">Date and time provider injected dependency.</param>
        /// <param name="nodeArgs">Node settings injected dependency.</param>
        /// <param name="mempoolPersistence">Memory pool persistence injected dependency.</param>
        /// <param name="loggerFactory">Logger factory injected dependency.</param>
        public MempoolManager(
            MempoolScheduler mempoolScheduler, 
            TxMempool memPool,
            IMempoolValidator validator, 
            MempoolOrphans orphans, 
            IDateTimeProvider dateTimeProvider, 
            NodeSettings nodeArgs, 
            IMempoolPersistence mempoolPersistence,
            ILoggerFactory loggerFactory)
        {
            this.MempoolScheduler = mempoolScheduler;
            this.memPool = memPool;
            this.DateTimeProvider = dateTimeProvider;
            this.NodeArgs = nodeArgs;
            this.Orphans = orphans;
            this.Validator = validator;
            this.mempoolPersistence = mempoolPersistence;
            this.mempoolLogger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Memory pool scheduler injected dependency.
        /// </summary>
        public MempoolScheduler MempoolScheduler { get; }

        /// <summary>
        /// Memory pool validator injected dependency.
        /// </summary>
        public IMempoolValidator Validator { get; } // public for testing

        /// <summary>
        /// Memory pool orphans injected dependency.
        /// </summary>
        public MempoolOrphans Orphans { get; } // public for testing

        /// <summary>
        /// Date time provider injected dependency.
        /// </summary>
        public IDateTimeProvider DateTimeProvider { get; }

        /// <summary>
        /// Node settings injected dependency.
        /// </summary>
        public NodeSettings NodeArgs { get; set; }

        /// <summary>
        /// Memory pool validator performance counter
        /// </summary>
        public MempoolPerformanceCounter PerformanceCounter => this.Validator.PerformanceCounter;

        #endregion

        #region Operations

        /// <summary>
        /// Gets the memory pool transactions asynchronously.
        /// </summary>
        /// <returns>Asynchronous task.</returns>
        public Task<List<uint256>> GetMempoolAsync()
        {
            return this.MempoolScheduler.ReadAsync(() => this.memPool.MapTx.Keys.ToList());
        }

        /// <summary>
        /// Gets a list of transaction information from the memory pool.
        /// </summary>
        /// <returns>List of transaction information.</returns>
        public List<TxMempoolInfo> InfoAll()
        {
            // TODO: DepthAndScoreComparator

            return this.memPool.MapTx.DescendantScore.Select(item => new TxMempoolInfo
            {
                Trx = item.Transaction,
                Time = item.Time,
                FeeRate = new FeeRate(item.Fee, (int)item.GetTxSize()),
                FeeDelta = item.ModifiedFee - item.Fee
            }).ToList();
        }

        /// <summary>
        /// Loads the memory pool asynchronously from a file.
        /// </summary>
        /// <param name="fileName">Filename to load from.</param>
        /// <returns>Asynchronous task.</returns>
        internal async Task LoadPool(string fileName = null)
        {
            if (this.mempoolPersistence != null && this.memPool?.MapTx != null && this.Validator != null)
            {
                this.mempoolLogger.LogInformation("Loading Memory Pool...");
                IEnumerable<MempoolPersistenceEntry> entries = this.mempoolPersistence.Load(fileName);
                int i = 0;
                if (entries != null)
                {
                    this.mempoolLogger.LogInformation($"...loaded {entries.Count()} cached entries.");
                    foreach (MempoolPersistenceEntry entry in entries)
                    {
                        Transaction trx = entry.Tx;
                        uint256 trxHash = trx.GetHash();
                        if (!this.memPool.Exists(trxHash))
                        {
                            MempoolValidationState state = new MempoolValidationState(false) { AcceptTime = entry.Time, OverrideMempoolLimit = true };
                            if (await this.Validator.AcceptToMemoryPoolWithTime(state, trx) && this.memPool.MapTx.ContainsKey(trxHash))
                            {
                                i++;
                                this.memPool.MapTx[trxHash].UpdateFeeDelta(entry.FeeDelta);
                            }
                        }
                    }
                    this.mempoolLogger.LogInformation($"...{i} entries accepted.");
                }
                else
                {
                    this.mempoolLogger.LogInformation($"...Unable to load memory pool cache from {fileName}.");
                }
            }
        }

        /// <summary>
        /// Saves the memory pool to persistent storage.
        /// </summary>
        /// <returns>Memory pool save result.</returns>
        internal MemPoolSaveResult SavePool()
        {
            if (this.mempoolPersistence == null)
                return MemPoolSaveResult.NonSuccess;
            return this.mempoolPersistence.Save(this.memPool);
        }

        /// <summary>
        /// Gets transaction information for a specific transaction.
        /// </summary>
        /// <param name="hash">Hash of the transaction to query.</param>
        /// <returns>Transaction information.</returns>
        public TxMempoolInfo Info(uint256 hash)
        {
            TxMempoolEntry item = this.memPool.MapTx.TryGet(hash);
            return item == null ? null : new TxMempoolInfo
            {
                Trx = item.Transaction,
                Time = item.Time,
                FeeRate = new FeeRate(item.Fee, (int)item.GetTxSize()),
                FeeDelta = item.ModifiedFee - item.Fee
            };
        }

        /// <summary>
        /// Gets transaction information asynchronously for all transactions in memory pool.
        /// </summary>
        /// <returns>Asynchronous task containing list of transaction information.</returns>
        public Task<List<TxMempoolInfo>> InfoAllAsync()
        {
            return this.MempoolScheduler.ReadAsync(this.InfoAll);

        }

        /// <summary>
        /// Gets transaction info asynchronously for a specific transaction in memory pool.
        /// </summary>
        /// <param name="hash">Hash of the transaction to query.</param>
        /// <returns>Asynchronous task containing transaction information.</returns>
        public Task<TxMempoolInfo> InfoAsync(uint256 hash)
        {
            return this.MempoolScheduler.ReadAsync(() => this.Info(hash));
        }

        /// <summary>
        /// Gets the memory pool size asynchronously.
        /// </summary>
        /// <returns>Asynchronous task containing memory pool size.</returns>
        public Task<long> MempoolSize()
        {
            return this.MempoolScheduler.ReadAsync(() => this.memPool.Size);
        }

        /// <summary>
        /// Asynchronously clears the memory pool.
        /// </summary>
        /// <returns>Asynchronous task.</returns>
        public Task Clear()
        {
            return this.MempoolScheduler.ReadAsync(() => this.memPool.Clear());
        }

        /// <summary>
        /// Gets the memory pool dynamic memory usage asynchronously.
        /// </summary>
        /// <returns>Asynchronous task containing the memory usage.</returns>
        public Task<long> MempoolDynamicMemoryUsage()
        {
            return this.MempoolScheduler.ReadAsync(() => this.memPool.DynamicMemoryUsage());
        }

        /// <summary>
        /// Removes transaction from a block in memory pool asynchronously.
        /// </summary>
        /// <param name="block">Block of transactions.</param>
        /// <param name="blockHeight">Location of the block.</param>
        /// <returns>Asynchronous task.</returns>
        public Task RemoveForBlock(Block block, int blockHeight)
        {
            //if (this.IsInitialBlockDownload)
            //	return Task.CompletedTask;

            return this.MempoolScheduler.WriteAsync(() =>
            {
                this.memPool.RemoveForBlock(block.Transactions, blockHeight);

                this.Validator.PerformanceCounter.SetMempoolSize(this.memPool.Size);
                this.Validator.PerformanceCounter.SetMempoolDynamicSize(this.memPool.DynamicMemoryUsage());
            });
        }

        #endregion
    }
}
