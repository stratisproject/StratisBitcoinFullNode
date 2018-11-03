using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public class RewindDataIndexStore : IRewindDataIndexStore
    {
        private readonly IDateTimeProvider dateTimeProvider;

        private readonly ICoinView coinView;

        private readonly IRewindDataIndexRepository rewindDataIndexRepository;

        private readonly ConcurrentDictionary<string, int> items;

        /// <summary>Time of the last cache flush.</summary>
        private DateTime lastCacheFlushTime;

        /// <summary>Length of the coinview cache flushing interval in seconds.</summary>
        /// <seealso cref="lastCacheFlushTime"/>
        public const int CacheFlushTimeIntervalSeconds = 60;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Performance counter to measure performance of the save and get operations.
        /// </summary>
        private readonly BackendPerformanceCounter performanceCounter;

        public RewindDataIndexStore(
            IDateTimeProvider dateTimeProvider,
            ICoinView coinView,
            ILoggerFactory loggerFactory,
            IRewindDataIndexRepository rewindDataIndexRepository)
        {
            Guard.NotNull(coinView, nameof(coinView));
            Guard.NotNull(rewindDataIndexRepository, nameof(rewindDataIndexRepository));

            this.items = new ConcurrentDictionary<string, int>();
            this.dateTimeProvider = dateTimeProvider;
            this.coinView = coinView;
            this.rewindDataIndexRepository = rewindDataIndexRepository;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
        }

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

        public async Task FlushAsync(bool force = true)
        {
            DateTime now = this.dateTimeProvider.GetUtcNow();
            if (!force && ((now - this.lastCacheFlushTime).TotalSeconds < CacheFlushTimeIntervalSeconds))
            {
                this.logger.LogTrace("(-)[NOT_NOW]");
                return;
            }

            IDictionary<string, int> itemsToSave = this.items.ToDictionary(i => i.Key, i => i.Value);
            await this.rewindDataIndexRepository.PutAsync(itemsToSave).ConfigureAwait(false);

            this.items.Clear();

            this.lastCacheFlushTime = this.dateTimeProvider.GetUtcNow();
        }

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
