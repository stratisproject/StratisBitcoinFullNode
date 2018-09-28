using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStore : IProvenBlockHeaderStore, IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Specification of the network the node runs on - RegTest/TestNet/MainNet.</summary>
        private readonly Network network;

        /// <summary>Allows consumers to perform clean-up during a graceful shutdown.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Chain state holds various information related to the status of the chain and its validation.</summary>
        private readonly IChainState chainState;

        /// <summary>The highest stored block in the repository.</summary>
        private ChainedHeader storeTip;

        /// <summary>Thread safe class representing a chain of headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Database repository storing <see cref="ProvenBlockHeader"></see>s.</summary>
        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        /// <summary>Lock object to protect access to <see cref="ProvenBlockHeader"/>.</summary>
        private readonly AsyncLock lockobj;

        /// <summary>Queue which contains blocks that should be saved to the database.</summary>
        private readonly AsyncQueue<KeyValuePair<HashHeightPair, ProvenBlockHeader>> provenBlockHeaderQueue;

        /// <summary>Batch of <see cref="ProvenBlockHeader"/> items which will be saved to the database.</summary>
        /// <remarks>Write access should be protected by <see cref="lockobj"/>.</remarks>
        private readonly ConcurrentDictionary<HashHeightPair, ProvenBlockHeader> batch;

        /// <summary>Task that runs <see cref="DequeueContinuouslyAsync"/>.</summary>
        private Task dequeueLoopTask;

        /// <summary>Maximum interval between saving batches.</summary>
        /// <remarks>Interval value is a prime number that wasn't used as an interval in any other component. That prevents having CPU consumption spikes.</remarks>
        private const int BatchMaxSaveIntervalSeconds = 47;

        /// <summary>Maximum number of bytes the batch can hold until the downloaded blocks are stored to the disk.</summary>
        internal const int BatchThresholdSizeBytes = 5 * 1000 * 1000;

        /// <summary>The current batch size in bytes.</summary>
        private long currentBatchSizeBytes;

        /// <summary>Performance counter to measure performance of the save and query operation.</summary>
        private readonly BackendPerformanceCounter performanceCounter;
        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>Current block tip hash and height that the <see cref= "ProvenBlockHeader"/> belongs to.</summary>
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="chain">Thread safe class representing a chain of headers from genesis</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">Persistent interface of the <see cref="ProvenBlockHeader"></see> DBreeze repository.</param>
        /// <param name="nodeLifetime">Allows consumers to perform clean-up during a graceful shutdown.</param>
        /// <param name="chainState">Chain state holds various information related to the status of the chain and its validation.</param>
        /// <param name="nodeStats">Registers an action used to append node stats when collected.</param>
        public ProvenBlockHeaderStore(
            Network network, 
            ConcurrentChain chain, 
            IDateTimeProvider dateTimeProvider, 
            ILoggerFactory loggerFactory, 
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeLifetime nodeLifetime,
            IChainState chainState,
            INodeStats nodeStats)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(provenBlockHeaderRepository, nameof(provenBlockHeaderRepository));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.chain = chain;
            this.provenBlockHeaderRepository = provenBlockHeaderRepository;
            this.nodeLifetime = nodeLifetime;
            this.chainState = chainState;
            this.lockobj = new AsyncLock();
            this.batch = new ConcurrentDictionary<HashHeightPair, ProvenBlockHeader>();
            this.provenBlockHeaderQueue = new AsyncQueue<KeyValuePair<HashHeightPair, ProvenBlockHeader>>();

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            this.logger.LogInformation("Initializing {0}.", nameof(ProvenBlockHeaderStore));

            await this.provenBlockHeaderRepository.InitializeAsync().ConfigureAwait(false);

            this.TipHashHeight = await this.GetTipHashHeightAsync().ConfigureAwait(false);

            this.logger.LogDebug("Initialized ProvenBlockHader block tip at '{0}'.", this.TipHashHeight);

            this.SetStoreTip(this.chain.GetBlock(this.TipHashHeight.Height));

            this.currentBatchSizeBytes = 0;

            this.dequeueLoopTask = this.DequeueContinuouslyAsync();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task LoadAsync()
        {
            this.logger.LogTrace("()");

            HashHeightPair tip = await this.provenBlockHeaderRepository.GetTipHashHeightAsync().ConfigureAwait(false);

            ChainedHeader next = this.chain.GetBlock(tip.Height);

            using (await this.lockobj.LockAsync().ConfigureAwait(false))
            {
                if (next == null)
                {
                    this.logger.LogTrace("(-)[NULL_NEXT_CHAINED_HEADER]");
                    return;
                }

                var blockHeights = new List<int>();

                while (next != null)
                {
                    blockHeights.Add(next.Height);

                    next = next.Previous;
                }

                var items = new Dictionary<int, ProvenBlockHeader>();

                items = await this.provenBlockHeaderRepository.GetAsync(1, tip.Height).ConfigureAwait(false);

                foreach (KeyValuePair<int, ProvenBlockHeader> item in items)
                {
                    var hashHeightPair = new HashHeightPair(this.chain.GetBlock(item.Key).HashBlock, item.Key);

                    this.provenBlockHeaderQueue.Enqueue(new KeyValuePair<HashHeightPair, ProvenBlockHeader>(hashHeightPair, item.Value));
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHeight), blockHeight);

            ProvenBlockHeader item = null;

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                item = await this.provenBlockHeaderRepository.GetAsync(blockHeight).ConfigureAwait(false);
            }

            if (item != null)
                this.logger.LogTrace("(-):*.{0}='{1}'", nameof(item), item);
            else
                this.logger.LogTrace("(-):null");

            Guard.Assert(item != null);

            return item;
        }

        public async Task<Dictionary<int, ProvenBlockHeader>> GetAsync(int fromBlockHeight, int toBlockHeight)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(fromBlockHeight), fromBlockHeight);
            this.logger.LogTrace("({0}:'{1}')", nameof(toBlockHeight), toBlockHeight);

            var items = new Dictionary<int, ProvenBlockHeader>();

            using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
            {
                // TODO : Get the items from cache once implemented.
                items = await this.provenBlockHeaderRepository.GetAsync(fromBlockHeight, toBlockHeight).ConfigureAwait(false);
            }

            return items;
        }

        /// <inheritdoc />
        public async Task<HashHeightPair> GetTipHashHeightAsync()
        {
            this.logger.LogTrace("()");

            this.TipHashHeight = await this.provenBlockHeaderRepository.GetTipHashHeightAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");

            return this.TipHashHeight;
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetTipAsync()
        {
            this.logger.LogTrace("()");

            this.TipHashHeight = await this.GetTipHashHeightAsync();

            this.logger.LogTrace("(-)");

            return await this.GetAsync(this.TipHashHeight.Height).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void AddToPending(ProvenBlockHeader provenBlockHeader, HashHeightPair newTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(provenBlockHeader), provenBlockHeader);

            this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip);

            lock (this.lockobj)
            {
                this.batch.TryAdd(newTip, provenBlockHeader);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Dequeues the blocks continuously and saves them to the database when the maximum batch size is reached or timer ran out.
        /// </summary>
        /// <remarks>Batch is always saved on shutdown.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task DequeueContinuouslyAsync()
        {
            this.logger.LogTrace("()");

            Task<KeyValuePair<HashHeightPair, ProvenBlockHeader>> dequeueTask = null;

            Task timerTask = null;

            while(!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Start a new dequeue task if not started already.
                dequeueTask = dequeueTask ?? this.provenBlockHeaderQueue.DequeueAsync();

                // Wait for one of the task, dequeue or timer (if available to finish).
                Task task = (timerTask == null) ? dequeueTask : await Task.WhenAny(dequeueTask, timerTask).ConfigureAwait(false);

                bool saveBatch = false;

                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Happens when node is shutting down or Dispose() is called.
                    // We want to save whatever is in the batch before exiting the loop.
                    saveBatch = true;

                    this.logger.LogDebug("Node is shutting down. Save batch.");
                }

                // Save batch if timer ran out or we've dequeued a new block and reached the consensus tip
                // or the maximum batch size is reached or the node is shutting down.
                if (dequeueTask.Status == TaskStatus.RanToCompletion)
                {
                    KeyValuePair<HashHeightPair, ProvenBlockHeader> item = dequeueTask.Result;

                    // Set the dequeue task to null so it can be assigned on the next iteration.
                    dequeueTask = null;

                    using (await this.lockobj.LockAsync().ConfigureAwait(false))
                    {  
                        this.batch.TryAdd(item.Key, item.Value);
                    }

                    this.currentBatchSizeBytes += item.Value.HeaderSize;

                    saveBatch = saveBatch || (this.currentBatchSizeBytes >= BatchThresholdSizeBytes) || this.chainState.IsAtBestChainTip;
                }
                else
                {
                    // Will be executed in case timer ran out or node is being shut down.
                    saveBatch = true;
                }

                if (saveBatch)
                {
                    await this.SaveBatchAsync().ConfigureAwait(false);
                    timerTask = null;
                }
                else
                {
                    // Start timer if it is not started already.
                    timerTask = timerTask ?? Task.Delay(BatchMaxSaveIntervalSeconds * 1000, this.nodeLifetime.ApplicationStopping);
                }

                await this.SaveBatchAsync().ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Saves items to the <see cref="ProvenBlockHeaderRepository"/>.</summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        private async Task SaveBatchAsync()
        {
            this.logger.LogTrace("()");

            if ((this.batch.Count != 0) && (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested))
            {
                using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
                {
                    using (await this.lockobj.LockAsync().ConfigureAwait(false))
                    {
                        await this.provenBlockHeaderRepository.PutAsync(this.batch.Select(kvp => kvp.Value).ToList(), this.batch.FirstOrDefault().Key).ConfigureAwait(false);

                        this.batch.Clear();
                    }
                }

                this.currentBatchSizeBytes = 0;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Sets the internal store tip and exposes the store tip to other components through the chain state.</summary>
        /// <param name="newTip">Tip to set the <see cref="ChainedHeader"/> store and the <see cref="IChainState.BlockStoreTip"/>.</param>
        private void SetStoreTip(ChainedHeader newTip)
        {
            this.logger.LogTrace("()");

            this.storeTip = newTip;

            this.chainState.BlockStoreTip = newTip;

            this.logger.LogTrace("(-)");
        }

        private void AddBenchStats(StringBuilder benchLog)
        {
            this.logger.LogTrace("()");

            benchLog.AppendLine("======ProvenBlockHeaderStore Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                benchLog.AppendLine(snapShot.ToString());
            else
                benchLog.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockobj.Dispose();
        }
    }
}