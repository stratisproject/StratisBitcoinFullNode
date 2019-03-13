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
    public class RewindDataIndexCache : IRewindDataIndexCache
    {
        private readonly Network network;

        /// <summary>
        /// Internal cache for rewind data index. Key is a TxId + N (N is an index of output in a transaction)
        /// and value is a rewind data index.
        /// </summary>
        private readonly Dictionary<RewindDataIndexItem, int> items;
        private readonly object itemsLock;

        /// <summary>
        /// Number of blocks to keep in cache after the flush.
        /// The number of items stored in cache is the sum of inputs used in every transaction in each of those blocks.
        /// </summary>
        private int numberOfBlocksToKeep;

        /// <summary>
        /// Performance counter to measure performance of the save and get operations.
        /// </summary>
        private readonly BackendPerformanceCounter performanceCounter;

        public RewindDataIndexCache()
        {
            this.itemsLock = new object();
        }

        public RewindDataIndexCache(IDateTimeProvider dateTimeProvider, Network network) : this()
        {
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));

            this.network = network;

            lock (this.itemsLock)
            {
                this.items = new Dictionary<RewindDataIndexItem, int>();
            }

            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);
        }

        /// <inheritdoc />
        public async Task InitializeAsync(int tipHeight, ICoinView coinView)
        {
            lock (this.itemsLock)
            {
                this.items.Clear();
            }

            this.numberOfBlocksToKeep = (int)this.network.Consensus.MaxReorgLength;

            int heightToSyncTo = tipHeight > this.numberOfBlocksToKeep ? tipHeight - this.numberOfBlocksToKeep : 1;

            for (int rewindHeight = tipHeight; rewindHeight >= heightToSyncTo; rewindHeight--)
            {
                RewindData rewindData = await coinView.GetRewindData(rewindHeight).ConfigureAwait(false);

                this.AddRewindData(rewindHeight, rewindData);
            }
        }

        /// <summary>
        /// Adding rewind information for a block in to the cache, we only add the unspent outputs.
        /// The cache key is [trxid-outputIndex] and the value is the height of the block on with the rewind data information is kept.
        /// </summary>
        /// <param name="rewindHeight">Height of the rewind data.</param>
        /// <param name="rewindData">The data itself</param>
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
                for (uint outputIndex = 0; outputIndex < unspent.Outputs.Length; outputIndex++)
                {
                    string key = $"{unspent.TransactionId}-{outputIndex}";
                    RewindDataIndexItem itemKey = new RewindDataIndexItem(unspent.TransactionId, outputIndex);

                    lock (this.itemsLock)
                    {
                        if (!this.items.ContainsKey(itemKey))
                        {
                            this.items.Add(itemKey, rewindHeight);
                        }
                        else
                        {
                            this.items[itemKey] = rewindHeight;
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task Remove(int tipHeight, ICoinView coinView)
        {
            this.Flush(tipHeight);

            int bottomHeight = tipHeight > this.numberOfBlocksToKeep ? tipHeight - this.numberOfBlocksToKeep : 1;

            RewindData rewindData = await coinView.GetRewindData(bottomHeight).ConfigureAwait(false);
            this.AddRewindData(bottomHeight, rewindData);
        }

        /// <inheritdoc />
        public void Save(Dictionary<RewindDataIndexItem, int> indexData)
        {
            using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
            {
                foreach (RewindDataIndexItem itemKey in indexData.Keys)
                {
                    lock (this.itemsLock)
                    {
                        if (!this.items.ContainsKey(itemKey))
                        {
                            this.items.Add(itemKey, indexData[itemKey]);
                        }
                        else
                        {
                            this.items[itemKey] = indexData[itemKey];
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Flush(int tipHeight)
        {
            int heightToKeepItemsTo = tipHeight > this.numberOfBlocksToKeep ? tipHeight - this.numberOfBlocksToKeep : 1; ;

            lock (this.itemsLock)
            {
                RewindDataIndexItem[] listOfItems = this.items.Keys.ToArray();
                foreach (RewindDataIndexItem itemKey in listOfItems)
                {
                    if ((this.items[itemKey] < heightToKeepItemsTo) || (this.items[itemKey] > tipHeight))
                    {
                        this.items.Remove(itemKey);
                    }
                }
            }
        }

        /// <inheritdoc />
        public int? Get(uint256 transactionId, uint transactionOutputIndex)
        {
            RewindDataIndexItem itemKey = new RewindDataIndexItem(transactionId, transactionOutputIndex);

            lock (this.itemsLock)
            {
                if (this.items.ContainsKey(itemKey))
                    return this.items[itemKey];
            }

            return null;
        }
    }
}
