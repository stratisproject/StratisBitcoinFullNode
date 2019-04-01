using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Transaction = DBreeze.Transactions.Transaction;

namespace Stratis.Bitcoin.AddressIndexing
{
    public class AddressIndexer : IDisposable
    {
        private readonly ISignals signals;

        private readonly DBreezeEngine dBreeze;

        private readonly DBreezeSerializer dBreezeSerializer;

        private readonly NodeSettings nodeSettings;

        private readonly Network network;

        private readonly IBlockStore blockStore;

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        internal const string TableName = "Data";

        internal const string TipKey = "Txindexertip";

        private HashHeightPair tip;

        private SubscriptionToken blockConnectedSubscription, blockDisconnectedSubscription;

        /// <summary>Queue that is populated when block is connected or disconnected.</summary>
        /// <remarks><c>bool</c> key is <c>true</c> when block is connected.</remarks>
        private Queue<KeyValuePair<bool, ChainedHeaderBlock>> blockReceivedQueue;

        /// <summary>Protects access to <see cref="blockReceivedQueue"/>.</summary>
        private readonly object lockObj;

        private readonly CancellationTokenSource cancellation;

        private Task queueProcessingTask;

        public AddressIndexer(NodeSettings nodeSettings, ISignals signals, DataFolder dataFolder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer,
            Network network, IBlockStore blockStore, INodeStats nodeStats)
        {
            this.signals = signals;
            this.dBreezeSerializer = dBreezeSerializer;
            this.nodeSettings = nodeSettings;
            this.network = network;
            this.blockStore = blockStore;
            this.nodeStats = nodeStats;

            this.cancellation = new CancellationTokenSource();
            this.lockObj = new object();
            this.dBreeze = new DBreezeEngine(dataFolder.AddrIndexPath);
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

                this.blockReceivedQueue = new Queue<KeyValuePair<bool, ChainedHeaderBlock>>();

                // Subscribe to events.
                this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(blockConnectedData =>
                {
                    lock (this.lockObj)
                    {
                        this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(true, blockConnectedData.ConnectedBlock));
                    }
                });

                this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(blockDisconnectedData =>
                {
                    lock (this.lockObj)
                    {
                        this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(false, blockDisconnectedData.DisconnectedBlock));
                    }
                });

                this.queueProcessingTask = this.ProcessQueueContinuouslyAsync();

                this.nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, 400);
            }
        }

        private void AddComponentStats(StringBuilder benchLog)
        {
            benchLog.AppendLine();
            benchLog.AppendLine("======AddressIndexer======");

            benchLog.AppendLine($"Unprocessed blocks: {this.blockReceivedQueue.Count}");
        }

        /// <summary>Continuously processes <see cref="blockReceivedQueue"/>.</summary>
        private async Task ProcessQueueContinuouslyAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                bool wait;

                lock (this.lockObj)
                {
                    wait = this.blockReceivedQueue.Count == 0;
                }

                if (wait)
                {
                    try
                    {
                        await Task.Delay(1000, this.cancellation.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    continue;
                }

                using (Transaction dbreezeTransaction = this.dBreeze.GetTransaction())
                {
                    int itemsProcessed = 0;

                    while (true)
                    {
                        // Dequeue the items.
                        KeyValuePair<bool, ChainedHeaderBlock> item;

                        lock (this.lockObj)
                        {
                            if (this.blockReceivedQueue.Count == 0)
                                break;

                            item = this.blockReceivedQueue.Dequeue();
                        }

                        try
                        {
                            this.ProcessBlockAddedOrRemoved(item, dbreezeTransaction);
                        }
                        catch (Exception e)
                        {
                            this.logger.LogError(e.ToString());
                            throw;
                        }

                        itemsProcessed++;

                        if (itemsProcessed > 100)
                            break;
                    }

                    dbreezeTransaction.Commit();
                }
            }
        }

        /// <summary>Processes block that was added or removed from consensus chain.</summary>
        private void ProcessBlockAddedOrRemoved(KeyValuePair<bool, ChainedHeaderBlock> item, Transaction dbreezeTransaction)
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

            Block block = item.Value.Block;
            int currentHeight = item.Value.ChainedHeader.Height;

            // Process inputs
            var inputs = new List<TxIn>();

            // Collect all inputs excluding coinbases.
            foreach (TxInList inputsCollection in block.Transactions.Where(x => !x.IsCoinBase).Select(x => x.Inputs))
                inputs.AddRange(inputsCollection);

            NBitcoin.Transaction[] transactions = this.blockStore.GetTransactionsByIds(inputs.Select(x => x.PrevOut.Hash).ToArray());

            for (int i = 0; i < inputs.Count; i++)
            {
                TxIn currentInput = inputs[i];

                TxOut txOut = transactions[i].Outputs[currentInput.PrevOut.N];

                // Address from which money were spent.
                BitcoinAddress address = txOut.ScriptPubKey.GetDestinationAddress(this.network);

                if (address == null)
                {
                    this.logger.LogTrace("Address wasn't recognized. ScriptPubKey: '{0}'.", txOut.ScriptPubKey);
                    continue;
                }

                Money amountSpent = txOut.Value;

                Row<byte[], byte[]> addrDataRow = dbreezeTransaction.Select<byte[], byte[]>(TableName, address.ToString().ToBytes());

                AddressIndexData addressIndexData = this.dBreezeSerializer.Deserialize<AddressIndexData>(addrDataRow.Value);

                if (blockAdded)
                {
                    // Record money being spent.
                    addressIndexData.AddressBalanceChanges.Add(new AddressBalanceChange()
                    {
                        Height = currentHeight,
                        Amount = amountSpent,
                        Deposited = false
                    });
                }
                else
                {
                    // Remove changes.
                    foreach (AddressBalanceChange change in addressIndexData.AddressBalanceChanges.Where(x => x.Height == currentHeight).ToList())
                        addressIndexData.AddressBalanceChanges.Remove(change);
                }

                dbreezeTransaction.Insert<byte[], byte[]>(TableName, address.ToString().ToBytes(), this.dBreezeSerializer.Serialize(addressIndexData));
            }

            // Process outputs.
            foreach (NBitcoin.Transaction tx in block.Transactions)
            {
                foreach (TxOut txOut in tx.Outputs)
                {
                    BitcoinAddress address = txOut.ScriptPubKey.GetDestinationAddress(this.network);

                    if (address == null)
                    {
                        this.logger.LogTrace("Address wasn't recognized. ScriptPubKey: '{0}'.", txOut.ScriptPubKey);
                        continue;
                    }

                    Money amountReceived = txOut.Value;

                    Row<byte[], byte[]> addrDataRow = dbreezeTransaction.Select<byte[], byte[]>(TableName, address.ToString().ToBytes());
                    AddressIndexData addressIndexData = addrDataRow.Exists ?
                        this.dBreezeSerializer.Deserialize<AddressIndexData>(addrDataRow.Value) :
                        new AddressIndexData() { AddressBalanceChanges = new List<AddressBalanceChange>()};

                    if (blockAdded)
                    {
                        // Record money being sent.
                        addressIndexData.AddressBalanceChanges.Add(new AddressBalanceChange()
                        {
                            Height = currentHeight,
                            Amount = amountReceived,
                            Deposited = true
                        });
                    }
                    else
                    {
                        // Remove changes.
                        foreach (AddressBalanceChange change in addressIndexData.AddressBalanceChanges.Where(x => x.Height == currentHeight).ToList())
                            addressIndexData.AddressBalanceChanges.Remove(change);
                    }

                    dbreezeTransaction.Insert<byte[], byte[]>(TableName, address.ToString().ToBytes(), this.dBreezeSerializer.Serialize(addressIndexData));
                }
            }

            this.tip = new HashHeightPair(item.Value.ChainedHeader.HashBlock, currentHeight);
            this.SaveTip(this.tip, dbreezeTransaction);
        }

        private HashHeightPair LoadTip()
        {
            using (Transaction transaction = this.dBreeze.GetTransaction())
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
        /// <returns>Balance of a given address or <c>null</c> if address wasn't indexed.</returns>
        public Money GetAddressBalance(BitcoinAddress address, int minConfirmations = 0)
        {
            if (this.tip == null)
                throw new IndexerNotInitializedException();

            throw new NotImplementedException();
        }

        /// <summary>Returns the total amount received by the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        /// <returns>Total amount received by a given address or <c>null</c> if address wasn't indexed.</returns>
        public Money GetReceivedByAddress(BitcoinAddress address, int minConfirmations = 0)
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

            this.cancellation.Cancel();

            this.queueProcessingTask.GetAwaiter().GetResult();

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
