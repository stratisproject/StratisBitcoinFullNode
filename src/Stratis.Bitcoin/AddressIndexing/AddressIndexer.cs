using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using DBreeze.Utils;
using LiteDB;
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

        private readonly NodeSettings nodeSettings;

        private readonly Network network;

        private readonly IBlockStore blockStore;

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        private readonly DataFolder dataFolder;

        private readonly IKeyValueRepository kvRepo;

        private HashHeightPair tip;

        private const string TipKey = "AddressIndexerTip";

        private SubscriptionToken blockConnectedSubscription, blockDisconnectedSubscription;

        /// <summary>Queue that is populated when block is connected or disconnected.</summary>
        /// <remarks><c>bool</c> key is <c>true</c> when block is connected.</remarks>
        private AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>> blockReceivedQueue;

        private LiteDatabase db;

        public AddressIndexer(NodeSettings nodeSettings, ISignals signals, DataFolder dataFolder, ILoggerFactory loggerFactory,
            Network network, IBlockStore blockStore, INodeStats nodeStats, IKeyValueRepository kvRepo)
        {
            this.signals = signals;
            this.nodeSettings = nodeSettings;
            this.network = network;
            this.blockStore = blockStore;
            this.nodeStats = nodeStats;
            this.dataFolder = dataFolder;
            this.kvRepo = kvRepo;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (this.nodeSettings.TxIndex)
            {
                string dbPath = Path.Combine(this.dataFolder.RootPath, "addressindex.litedb");
                this.db = new LiteDatabase(new ConnectionString() {Filename = dbPath});

                this.logger.LogDebug("TxIndexing is enabled.");
                this.tip = this.kvRepo.LoadValue<HashHeightPair>(TipKey);

                if (this.tip == null)
                {
                    this.logger.LogDebug("Tip was not found, initializing with genesis.");
                    this.tip = new HashHeightPair(this.network.GenesisHash, 0);
                }

                this.blockReceivedQueue = new AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>>(this.OnEnqueueAsync);

                // Subscribe to events.
                this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(blockConnectedData =>
                {
                    this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(true, blockConnectedData.ConnectedBlock));
                });

                this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(blockDisconnectedData =>
                {
                    this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(false, blockDisconnectedData.DisconnectedBlock));
                });

                this.nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, 400);
            }
        }

        private Task OnEnqueueAsync(KeyValuePair<bool, ChainedHeaderBlock> item, CancellationToken cancellationtoken)
        {
            try
            {
                this.ProcessBlockAddedOrRemoved(item);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());
                throw;
            }

            return Task.CompletedTask;
        }

        private void AddComponentStats(StringBuilder benchLog)
        {
            benchLog.AppendLine();
            benchLog.AppendLine("======AddressIndexer======");

            benchLog.AppendLine($"Unprocessed blocks: {this.blockReceivedQueue.Count}");
        }

        /// <summary>Processes block that was added or removed from consensus chain.</summary>
        private void ProcessBlockAddedOrRemoved(KeyValuePair<bool, ChainedHeaderBlock> item)
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

                LiteCollection<AddressBalanceChange> addressChanges = this.db.GetCollection<AddressBalanceChange>(address.ToString());

                if (blockAdded)
                {
                    // Record money being spent.
                    addressChanges.Insert(new AddressBalanceChange()
                    {
                        Height = currentHeight,
                        Amount = amountSpent,
                        Deposited = false
                    });
                }
                else
                {
                    // Remove changes.
                    addressChanges.Delete(x => x.Height == currentHeight);
                }
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

                    LiteCollection<AddressBalanceChange> addressChanges = this.db.GetCollection<AddressBalanceChange>(address.ToString());

                    if (blockAdded)
                    {
                        // Record money being sent.
                        addressChanges.Insert(new AddressBalanceChange()
                        {
                            Height = currentHeight,
                            Amount = amountReceived,
                            Deposited = true
                        });
                    }
                    else
                    {
                        // Remove changes.
                        addressChanges.Delete(x => x.Height == currentHeight);
                    }
                }
            }

            this.tip = new HashHeightPair(item.Value.ChainedHeader.HashBlock, currentHeight);
            this.kvRepo.SaveValue(TipKey, this.tip);
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

            this.db?.Dispose();
        }
    }

    public class IndexerNotInitializedException : Exception
    {
        public IndexerNotInitializedException() : base("Component wasn't initialized and is not ready to use. Make sure -txindex is set to true.")
        {
        }
    }
}
