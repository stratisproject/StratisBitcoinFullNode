using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    /// <inheritdoc />
    public class RewindDataIndexStore : IRewindDataIndexStore
    {
        /// <summary>
        /// The coin view to be used for getting rewind data.
        /// </summary>
        private readonly ICoinView coinView;

        /// <summary>
        /// The chained header tree.
        /// </summary>
        private readonly IChainedHeaderTree chainedHeaderTree;

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

        public RewindDataIndexStore(IDateTimeProvider dateTimeProvider, ICoinView coinView, IChainedHeaderTree chainedHeaderTree)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(coinView, nameof(coinView));
            Guard.NotNull(chainedHeaderTree, nameof(chainedHeaderTree));

            this.items = new ConcurrentDictionary<string, int>();
            this.coinView = coinView;
            this.chainedHeaderTree = chainedHeaderTree;

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
        }

        public async Task InitializeAsync()
        {
            ChainedHeader tip = this.chainedHeaderTree.GetBestPeerTip();
            int heightToSyncTo = tip.Height > NumberOfItemsToKeep ? tip.Height - NumberOfItemsToKeep : 0;

            for (int i = tip.Height; i >= heightToSyncTo; i--)
            {
                RewindData rewindData = await this.coinView.GetRewindData(i).ConfigureAwait(false);
                if (rewindData?.OutputsToRestore == null || rewindData.OutputsToRestore.Count == 0) continue;

                foreach (UnspentOutputs unspent in rewindData.OutputsToRestore)
                {
                    for (int outputIndex = 0; outputIndex < unspent.Outputs.Length; outputIndex++)
                    {
                        string key = $"{unspent.TransactionId}-{outputIndex}";
                        this.items[key] = checked((int)unspent.Height);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Save(Dictionary<string, int> indexData)
        {
            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                foreach (KeyValuePair<string, int> indexRecord in indexData)
                {
                    this.items[indexRecord.Key] = indexRecord.Value;
                }
            }
        }

        /// <inheritdoc />
        public void Flush()
        {
            int maxStoredHeight = this.items.Select(i => i.Value).DefaultIfEmpty(0).Max();
            int heightToKeepItemsTo = maxStoredHeight - NumberOfItemsToKeep;

            if (heightToKeepItemsTo <= 0) return;

            List<KeyValuePair<string, int>> itemsToRemove = this.items.Where(i => i.Value < heightToKeepItemsTo).ToList();
            foreach (KeyValuePair<string, int> item in itemsToRemove)
            {
                this.items.TryRemove(item.Key, out int unused);
            }


        }

        /// <inheritdoc />
        public int? Get(uint256 transactionId, int transactionOutputIndex)
        {
            string key = $"{transactionId}-{transactionOutputIndex}";
            
            if (this.items.TryGetValue(key, out int rewindDataIndex))
                return rewindDataIndex;

            return null;
        }
    }
}
