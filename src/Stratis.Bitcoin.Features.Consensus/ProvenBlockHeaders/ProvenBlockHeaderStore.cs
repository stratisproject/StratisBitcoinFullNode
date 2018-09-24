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

        private readonly ProvenBlockHeader provenBlockHeader;

        private readonly ConcurrentChain chain;

        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        private readonly int threshold;

        private readonly int thresholdWindow;

        private readonly ConcurrentDictionary<uint256, ProvenBlockHeader> items = 
            new ConcurrentDictionary<uint256, ProvenBlockHeader>();

        /// <summary>Lock object to protect access to <see cref="ProvenBlockHeader"/>.</summary>
        private readonly AsyncLock lockobj;

        public long Count
        {
            get
            {
                return this.items.Count();
            }
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="chain">XXXXXXXXXXXXXXXXXXXXXXXXX</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="provenBlockHeaderRepository">XXXXXXXX7XXXXXXXXXXXXXXXXX</param>
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
                    this.items.TryAdd(stakeItem.BlockId, stakeItem.ProvenBlockHeader);
            }

            this.logger.LogTrace("(-)");
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // Pop some items to remove a window of 10 % of the threshold.
            throw new NotImplementedException();
        }

        public Task<ProvenBlockHeader> GetAsync(uint256 blockId, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<ProvenBlockHeader> GetTipAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task SetAsync(ProvenBlockHeader provenBlockHeader, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }        

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockobj.Dispose();
        }
    }
}
