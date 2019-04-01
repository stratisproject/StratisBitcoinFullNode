using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

        private readonly ILogger logger;

        internal const string TableName = "Data";

        internal const string TipKey = "Txindexertip";

        private HashHeightPair tip;

        private SubscriptionToken blockConnectedSubscription, blockDisconnectedSubscription;

        /// <summary>Queue that is populated when block is connected or disconnected.</summary>
        /// <remarks><c>bool</c> key is <c>true</c> when block is connected.</remarks>
        private AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>> blockReceivedQueue;

        public AddressIndexer(NodeSettings nodeSettings, ISignals signals, DataFolder dataFolder, ILoggerFactory loggerFactory, DBreezeSerializer dBreezeSerializer, Network network,
            IBlockStore blockStore)
        {
            this.signals = signals;
            this.dBreezeSerializer = dBreezeSerializer;
            this.nodeSettings = nodeSettings;
            this.network = network;
            this.blockStore = blockStore;

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
            int currentHeight = item.Value.ChainedHeader.Height;

            using (DBreeze.Transactions.Transaction dbreezeTransaction = this.dBreeze.GetTransaction())
            {
                // Process inputs
                var inputs = new List<TxIn>();

                foreach (TxInList inputsCollection in block.Transactions.Select(x => x.Inputs))
                    inputs.AddRange(inputsCollection);

                Transaction[] transactions = this.blockStore.GetTransactionsByIds(inputs.Select(x => x.PrevOut.Hash).ToArray());

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

                    Row<byte[], byte[]> addrDataRow = dbreezeTransaction.Select<byte[], byte[]>(TableName, address.ToBytes());
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
                }

                // Process outputs.
                foreach (Transaction tx in block.Transactions)
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

                        Row<byte[], byte[]> addrDataRow = dbreezeTransaction.Select<byte[], byte[]>(TableName, address.ToBytes());
                        AddressIndexData addressIndexData = this.dBreezeSerializer.Deserialize<AddressIndexData>(addrDataRow.Value);

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
                    }
                }

                this.tip = new HashHeightPair(item.Value.ChainedHeader.HashBlock, currentHeight);
                this.SaveTip(this.tip, dbreezeTransaction);

                dbreezeTransaction.Commit();
            }

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
