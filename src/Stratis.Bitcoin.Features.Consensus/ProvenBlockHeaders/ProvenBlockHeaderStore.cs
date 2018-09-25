using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
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
        private readonly AsyncQueue<StakeItem> stakeItemQueue;

        /// <summary>Batch of  <see cref="StakeItem"/> items which should be saved in the database.</summary>
        /// <remarks>Write access should be protected by <see cref="lockobj"/>.</remarks>
        private readonly List<StakeItem> batch;

        /// <summary>Task that runs <see cref="DequeueContinuouslyAsync"/>.</summary>
        private Task dequeueLoopTask;

        /// <summary>Maximum interval between saving batches.</summary>
        /// <remarks>Interval value is a prime number that wasn't used as an interval in any other component. That prevents having CPU consumption spikes.</remarks>
        private const int BatchMaxSaveIntervalSeconds = 47;

        /// <summary>Maximum number of bytes the batch can hold until the downloaded blocks are stored to the disk.</summary>
        internal const int BatchThresholdSizeBytes = 5 * 1000 * 1000;

        /// <summary>The current batch size in bytes.</summary>
        private long currentBatchSizeBytes;

        /// <summary>Current block tip hash that the <see cref= "ProvenBlockHeader"/> belongs to.</summary>
        public uint256 TipHash { get; private set; }

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
        public ProvenBlockHeaderStore(
            Network network, 
            ConcurrentChain chain, 
            IDateTimeProvider dateTimeProvider, 
            ILoggerFactory loggerFactory, 
            IProvenBlockHeaderRepository provenBlockHeaderRepository,
            INodeLifetime nodeLifetime,
            IChainState chainState)
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
            this.batch = new List<StakeItem>();
            this.stakeItemQueue = new AsyncQueue<StakeItem>();
        }

        /// <inheritdoc />
        public async Task InitializeAsync(uint256 blockHash = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            await this.provenBlockHeaderRepository.InitializeAsync(blockHash, cancellationToken).ConfigureAwait(false);

            this.TipHash = await this.GetTipHashAsync(cancellationToken).ConfigureAwait(false);
            this.logger.LogDebug("Initialized ProvenBlockHader block tip at '{0}'.", this.TipHash);

            this.SetStoreTip(this.chain.GetBlock(this.TipHash));

            this.currentBatchSizeBytes = 0;
            this.dequeueLoopTask = this.DequeueContinuouslyAsync();

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            uint256 hash = await this.provenBlockHeaderRepository.GetTipHashAsync().ConfigureAwait(false);
            ChainedHeader next = this.chain.GetBlock(hash);

            using (await this.lockobj.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (next == null)
                {
                    this.logger.LogTrace("(-)[NULL_NEXT_CHAINED_HEADER]");
                    return;
                }

                var load = new List<StakeItem>();

                while (next != null)
                {
                    load.Add(new StakeItem
                    {
                        BlockId = next.HashBlock,
                        Height = next.Height,
                    });

                    next = next.Previous;
                }

                await this.provenBlockHeaderRepository.GetAsync(load).ConfigureAwait(false);

                // All ProvenBlockHeader items should be in store.
                if (load.Any(l => l.ProvenBlockHeader == null))
                {
                    this.logger.LogTrace("(-)[PROVEN_BLOCK_HEADER_INFO_MISSING]");
                    throw new ConfigurationException("Missing proven block header information, delete the data folder and re-download the chain");
                }

                foreach (StakeItem stakeItem in load)
                    this.stakeItemQueue.Enqueue(stakeItem);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetAsync(uint256 blockId, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockId), blockId);

            List<StakeItem> item = new List<StakeItem>
            {
                new StakeItem
                {
                    BlockId = blockId
                }
            };

            await this.provenBlockHeaderRepository.GetAsync(item, cancellationToken).ConfigureAwait(false);

            var provenBlockHeader = item.FirstOrDefault().ProvenBlockHeader;

            if (provenBlockHeader != null)
                this.logger.LogTrace("(-):*.{0}='{1}'", nameof(provenBlockHeader), provenBlockHeader);
            else
                this.logger.LogTrace("(-):null");

            Guard.Assert(provenBlockHeader != null);

            return provenBlockHeader;
        }

        /// <inheritdoc />
        public async Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            this.TipHash = await this.provenBlockHeaderRepository.GetTipHashAsync(cancellationToken).ConfigureAwait(false);

            this.logger.LogTrace("(-)");

            return this.TipHash;
        }

        /// <inheritdoc />
        public async Task<ProvenBlockHeader> GetTipAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            this.TipHash = await this.GetTipHashAsync(cancellationToken);

            this.logger.LogTrace("(-)");

            return await this.GetAsync(this.TipHash).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void AddToPending(ChainedHeader chainedHeader, ProvenBlockHeader provenBlockHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(provenBlockHeader), provenBlockHeader);
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            var item = new StakeItem
            {
                BlockId = chainedHeader.HashBlock,
                Height = chainedHeader.Height,
                ProvenBlockHeader = provenBlockHeader,
                InStore = false
            };

            this.stakeItemQueue.Enqueue(item);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Dequeues the blocks continuously and saves them to the database when the maximum batch size is reached or timer ran out.
        /// </summary>
        /// <remarks>Batch is always saved on shutdown.</remarks>
        private async Task DequeueContinuouslyAsync()
        {
            this.logger.LogTrace("()");

            Task<StakeItem> dequeueTask = null;
            Task timerTask = null;

            while(!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Start a new dequeue task if not started already.
                dequeueTask = dequeueTask ?? this.stakeItemQueue.DequeueAsync();

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
                    StakeItem item = dequeueTask.Result;

                    // Set the dequeue task to null so it can be assigned on the next iteration.
                    dequeueTask = null;

                    using (await this.lockobj.LockAsync().ConfigureAwait(false))
                    {  
                        this.batch.Add(item);
                    }

                    this.currentBatchSizeBytes += item.ProvenBlockHeader.HeaderSize;

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
            }

            await this.SaveBatchAsync().ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Saves items to the <see cref="ProvenBlockHeaderRepository"/>.</summary>
        private async Task SaveBatchAsync()
        {
            this.logger.LogTrace("()");

            if (this.batch.Count != 0)
            {
                using (await this.lockobj.LockAsync().ConfigureAwait(false))
                {
                    await this.provenBlockHeaderRepository.PutAsync(this.batch).ConfigureAwait(false);

                    this.batch.Clear();
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

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockobj.Dispose();
        }
    }
}
