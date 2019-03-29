using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.TxIndexing
{
    // TODO THIS CLASS IS NOT FINISHED AND NOT USED
    public class TransactionIndexer : IDisposable
    {
        private readonly ISignals signals;

        private readonly DBreezeEngine dBreeze;

        private readonly DBreezeSerializer dBreezeSerializer;

        private readonly NodeSettings nodeSettings;

        private readonly Network network;

        private readonly ILogger logger;

        internal const string TableName = "Data";

        internal const string TipKey = "Txindexertip";

        private HashHeightPair tip;

        private SubscriptionToken blockConnectedSubscription, blockDisconnectedSubscription;

        /// <summary>Queue that is populated when block is connected or disconnected.</summary>
        /// <remarks><c>bool</c> key is <c>true</c> when block is connected.</remarks>
        private AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>> blockReceivedQueue;

        public TransactionIndexer(NodeSettings nodeSettings, ISignals signals, DataFolder dataFolder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer, Network network)
        {
            this.signals = signals;
            this.dBreezeSerializer = dBreezeSerializer;
            this.nodeSettings = nodeSettings;
            this.network = network;

            this.dBreeze = new DBreezeEngine(dataFolder.TxIndexPath);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (this.nodeSettings.TxIndex)
            {
                this.logger.LogDebug("TxIndexing is enabled.");

                this.tip = this.LoadTip();

                if (this.tip == null)
                {
                    this.logger.LogDebug("Tip was not found, initializing with genesis.");
                    this.tip = new HashHeightPair(this.network.GenesisHash, 0);
                }

                this.blockReceivedQueue = new AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>>(this.OnBlockAddedOrRemoved);

                // Subscribe to events.
                this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(blockConnectedData =>
                {
                    this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(true, blockConnectedData.ConnectedBlock));
                });

                this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(blockDisconnectedData =>
                {
                    this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(false, blockDisconnectedData.DisconnectedBlock));
                });
            }
        }

        /// <summary>Invoked when block was added or removed from consensus chain.</summary>
        private Task OnBlockAddedOrRemoved(KeyValuePair<bool, ChainedHeaderBlock> item, CancellationToken cancellationToken)
        {
            // Make sure it's on top of the tip.
            bool blockAdded = item.Key;

            if ((blockAdded && item.Value.ChainedHeader.Header.HashPrevBlock != this.tip.Hash) ||
                (!blockAdded && item.Value.ChainedHeader.HashBlock != this.tip.Hash))
            {
                const string message = "TransactionIndexer is in inconsistent state. This can happen if you've enabled txindex on an already synced or partially synced node. " +
                                       "Remove everything from the data folder and run the node with -txindex=true.";

                this.logger.LogCritical(message);
                this.logger.LogTrace("(-)[INCONSISTENT_STATE]");
                throw new Exception(message);
            }

            // TODO remove it later
            if (this.blockReceivedQueue.Count > 10)
            {
                this.logger.LogWarning("TxIndexer is lagging behind. {0} items are enqueued.", this.blockReceivedQueue.Count);
            }

            Block block = item.Value.Block;

            if (blockAdded)
            {
                // TODO process inputs
               //List<TxIn> q =  block.Transactions.Select(x => x.Inputs).First().ToList();
               //q.First().

                // take input, tx id from it, fetch txes from blockstore. from txes get address and value

                // TODO remove from inputs, add to outputs
            }

            // TODO class:
            // KV where key is address and value is a list of operations.
            //operation: type (spend\deposit), block height, amount

            // TODO
            // update index, save the tip

            return Task.CompletedTask;
        }

        private HashHeightPair LoadTip()
        {
            using (DBreeze.Transactions.Transaction transaction = this.dBreeze.GetTransaction())
            {
                Row<byte[], byte[]> tipRow = transaction.Select<byte[], byte[]>(TableName, TipKey.ToBytes());

                if (!tipRow.Exists)
                {
                    this.logger.LogTrace("(-)[NO_TIP]:null");
                    return null;
                }

                var loadedTip = this.dBreezeSerializer.Deserialize<HashHeightPair>(tipRow.Value);

                return loadedTip;
            }
        }

        private void SaveTip(HashHeightPair tipToSave, DBreeze.Transactions.Transaction transaction)
        {
            transaction.Insert<byte[], byte[]>(TableName, TipKey.ToBytes(), this.dBreezeSerializer.Serialize(tipToSave));
        }

        /// <summary>Returns balance of the given address confirmed with at least <paramref name="minConfirmations"/> confirmations.</summary>
        public Money GetAddressBalance(BitcoinAddress address, int minConfirmations = 0)
        {
            if (this.tip == null)
                throw new IndexerNotInitializedException();

            throw new NotImplementedException();
        }

        /// <summary>Returns the total amount received by the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        public Money GetReceivedByAddress(BitcoinAddress address, int minConfirmations = 0)
        {
            if (this.tip == null)
                throw new IndexerNotInitializedException();

            throw new NotImplementedException();
        }

        /// <summary>Returns the total amount spent from the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        public Money GetSpentByAddress(BitcoinAddress address, int minConfirmations = 0)
        {
            if (this.tip == null)
                throw new IndexerNotInitializedException();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.signals.Unsubscribe(this.blockConnectedSubscription);
            this.signals.Unsubscribe(this.blockDisconnectedSubscription);

            this.blockReceivedQueue.Dispose();

            this.dBreeze.Dispose();
        }
    }

    public class IndexerNotInitializedException : Exception
    {
        public IndexerNotInitializedException() : base("Component wasn't initialized and is not ready to use. Make sure -txindex is set to true.")
        {
        }
    }
}
