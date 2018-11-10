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
        private readonly ConcurrentDictionary<string, (int, bool)> items;

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

            this.items = new ConcurrentDictionary<string, (int, bool)>();
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
                    this.items[indexRecord.Key] = (indexRecord.Value, false);
                }
            }
        }

        /// <inheritdoc />
        public async Task FlushAsync()
        {
            IDictionary<string, int> itemsToSave = this.items.Where(d => d.Value.Item2 == false).ToDictionary(i => i.Key, i => i.Value.Item1);
            await this.rewindDataIndexRepository.PutAsync(itemsToSave).ConfigureAwait(false);

            if (this.items.Count <= NumberOfItemsToKeep) return;

            // Order by rewind data index and remove older records.
            List<KeyValuePair<string, (int, bool)>> orderedItems = this.items.OrderByDescending(i => i.Value.Item1).Skip(NumberOfItemsToKeep).ToList();
            foreach (KeyValuePair<string, (int, bool)> item in orderedItems)
            {
                this.items.TryRemove(item.Key, out (int, bool) unused);
            }
        }

        /// <inheritdoc />
        public async Task<int?> GetAsync(uint256 transactionId, int transactionOutputIndex)
        {
            string key = $"{transactionId}-{transactionOutputIndex}";
            
            if (this.items.TryGetValue(key, out (int, bool) rewindDataIndex))
                return rewindDataIndex.Item1;

            int? storedRewindDataIndex = await this.rewindDataIndexRepository.GetAsync(key);
            return storedRewindDataIndex;
        }
    }
}
