using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <inheritdoc />
    public class RewindDataIndexStore : IRewindDataIndexStore
    {
        /// <summary>
        /// The rewind data index repository that is used to store and retrieve data from the disk.
        /// </summary>
        private readonly IRewindDataIndexRepository rewindDataIndexRepository;

        /// <summary>
        /// Internal cache for rewind data index. Key is a TxId + N (N is an index of output in a transaction)
        /// and value is a rewind data index.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> items;

        /// <summary>
        /// Number of items to keep in cache after the flush.
        /// </summary>
        private const int NumberOfItemsToKeep = 500;

        /// <summary>
        /// Performance counter to measure performance of the save and get operations.
        /// </summary>
        private readonly BackendPerformanceCounter performanceCounter;

        public RewindDataIndexStore(
            IDateTimeProvider dateTimeProvider,
            IRewindDataIndexRepository rewindDataIndexRepository)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(rewindDataIndexRepository, nameof(rewindDataIndexRepository));

            this.items = new ConcurrentDictionary<string, int>();
            this.rewindDataIndexRepository = rewindDataIndexRepository;

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
        }

        /// <inheritdoc />
        public async Task SaveAsync(Dictionary<string, int> indexData)
        {
            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                foreach (KeyValuePair<string, int> indexRecord in indexData)
                {
                    this.items[indexRecord.Key] = indexRecord.Value;
                }

                // Save the items to disk.
                await this.rewindDataIndexRepository.PutAsync(indexData).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task FlushAsync()
        {
            IDictionary<string, int> itemsToSave = this.items.ToDictionary(i => i.Key, i => i.Value);
            await this.rewindDataIndexRepository.PutAsync(itemsToSave).ConfigureAwait(false);

            if (this.items.Count <= NumberOfItemsToKeep) return;

            // Order by rewind data index and remove older records.
            List<KeyValuePair<string, int>> orderedItems = this.items.OrderByDescending(i => i.Value).Skip(NumberOfItemsToKeep).ToList();
            foreach (KeyValuePair<string, int> item in orderedItems)
            {
                this.items.TryRemove(item.Key, out int unused);
            }

            this.items.Clear();
        }

        /// <inheritdoc />
        public async Task<int?> GetAsync(uint256 transactionId, int transactionOutputIndex)
        {
            string key = $"{transactionId}-{transactionOutputIndex}";
            
            if (this.items.TryGetValue(key, out int rewindDataIndex))
                return rewindDataIndex;

            int? storedRewindDataIndex = await this.rewindDataIndexRepository.GetAsync(key);
            return storedRewindDataIndex;
        }
    }
}
