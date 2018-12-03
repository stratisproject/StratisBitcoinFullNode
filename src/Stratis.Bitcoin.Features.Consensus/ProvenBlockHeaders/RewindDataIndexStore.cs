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
        private readonly Network network;

        /// <summary>
        /// Internal cache for rewind data index. Key is a TxId + N (N is an index of output in a transaction)
        /// and value is a rewind data index.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> items;

        /// <summary>
        /// Number of blocks to keep in cache after the flush.
        /// The number of items stored in cache is the sum of inputs used in every transaction in each of those blocks.
        /// </summary>
        private int numberOfBlocksToKeep;

        /// <summary>
        /// Performance counter to measure performance of the save and get operations.
        /// </summary>
        private readonly BackendPerformanceCounter performanceCounter;

        public RewindDataIndexStore(IDateTimeProvider dateTimeProvider, Network network)
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));

            this.network = network;

            this.items = new ConcurrentDictionary<string, int>();

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
        }

        /// <inheritdoc />
        public async Task InitializeAsync(int tipHeight, ICoinView coinView)
        {
            this.items.Clear();

            this.numberOfBlocksToKeep = (int)this.network.Consensus.MaxReorgLength;

            int heightToSyncTo = tipHeight > this.numberOfBlocksToKeep ? tipHeight - this.numberOfBlocksToKeep : 0;

            for (int rewindHeight = tipHeight - 1; rewindHeight >= heightToSyncTo; rewindHeight--)
            {
                RewindData rewindData = await coinView.GetRewindData(rewindHeight).ConfigureAwait(false);

                this.AddRewindData(rewindHeight, rewindData);
            }
        }

        private void AddRewindData(int rewindHeight, RewindData rewindData)
        {
            if (rewindData == null)
            {
                throw new ConsensusException($"Rewind data of height '{rewindHeight}' was not found!");
            }

            if (rewindData.OutputsToRestore == null || rewindData.OutputsToRestore.Count == 0)
            {
                return;
            }

            foreach (UnspentOutputs unspent in rewindData.OutputsToRestore)
            {
                for (int outputIndex = 0; outputIndex < unspent.Outputs.Length; outputIndex++)
                {
                    string key = $"{unspent.TransactionId}-{outputIndex}";
                    this.items[key] = rewindHeight;
                }
            }
        }

        /// <inheritdoc />
        public async Task Remove(int tipHeight, ICoinView coinView)
        {
            this.Flush(tipHeight);

            int heightToKeepItemsTo = tipHeight - this.numberOfBlocksToKeep;

            RewindData rewindData = await coinView.GetRewindData(heightToKeepItemsTo).ConfigureAwait(false);
            this.AddRewindData(heightToKeepItemsTo, rewindData);
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
        public void Flush(int tipHeight)
        {
            int heightToKeepItemsTo = tipHeight - this.numberOfBlocksToKeep;

            List<KeyValuePair<string, int>> listOfItems = this.items.ToList();
            foreach (KeyValuePair<string, int> item in listOfItems)
            {
                if ((item.Value < heightToKeepItemsTo) || (item.Value > tipHeight))
                {
                    this.items.TryRemove(item.Key, out int unused);
                }
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
