using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
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

        /// <summary>Thread safe class representing a chain of headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Database repository storing <see cref="ProvenBlockHeader"></see>s.</summary>
        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        private readonly int threshold;

        private readonly int thresholdWindow;

        private readonly ConcurrentDictionary<uint256, StakeItem> items =
            new ConcurrentDictionary<uint256, StakeItem>();

        /// <summary>Lock object to protect access to <see cref="ProvenBlockHeader"/>.</summary>
        private readonly AsyncLock lockobj;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="chain">Thread safe class representing a chain of headers from genesis</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">Persistent interface of the <see cref="ProvenBlockHeader"></see> DBreeze repository.</param>
        public ProvenBlockHeaderStore(Network network, ConcurrentChain chain, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, IProvenBlockHeaderRepository provenBlockHeaderRepository)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(chain, nameof(chain));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.chain = chain;
            this.provenBlockHeaderRepository = provenBlockHeaderRepository;
            this.threshold = 5000; // Count of items in memory.
            this.thresholdWindow = Convert.ToInt32(this.threshold * 0.4); // A window threshold.
            this.lockobj = new AsyncLock();
        }

        /// <summary>
        /// Loads <see cref="ProvenBlockHeader"/> items from disk.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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

                    if ((load.Count >= this.threshold) || next.Previous == null)
                        break;

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
                    this.items.TryAdd(stakeItem.BlockId, stakeItem);
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("()");

            int count = this.items.Count;

            if (count > this.threshold)
            {
                // Push to store all items that are not already persisted.
                ICollection<StakeItem> entries = this.items.Values;

                await this.provenBlockHeaderRepository.PutAsync(entries.Where(w => !w.InStore)).ConfigureAwait(false);

                // Pop some items to remove a window of 10 % of the threshold.
                ConcurrentDictionary<uint256, StakeItem> select = this.items;

                IEnumerable<KeyValuePair<uint256, StakeItem>> items = select.OrderBy(o => o.Value.Height).Take(this.thresholdWindow);

                StakeItem unused;

                foreach (KeyValuePair<uint256, StakeItem> item in items)
                    this.items.TryRemove(item.Key, out unused);
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
        public async Task<ProvenBlockHeader> GetTipAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            uint256 blockId = await this.provenBlockHeaderRepository.GetTipHashAsync(cancellationToken).ConfigureAwait(false);

            return await this.GetAsync(blockId).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SetAsync(ChainedHeader chainedHeader, ProvenBlockHeader provenBlockHeader, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(provenBlockHeader), provenBlockHeader);

            if (this.items.ContainsKey(chainedHeader.HashBlock))
            {
                this.logger.LogTrace("(-)[ALREADY_EXISTS]");
                return;
            }

            var item = new StakeItem
            {
                BlockId = chainedHeader.HashBlock,
                Height = chainedHeader.Height,
                ProvenBlockHeader = provenBlockHeader,
                InStore = false
            };

            bool added = this.items.TryAdd(chainedHeader.HashBlock, item);

            if (added)
                await this.FlushAsync(cancellationToken).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockobj.Dispose();
        }
    }
}
