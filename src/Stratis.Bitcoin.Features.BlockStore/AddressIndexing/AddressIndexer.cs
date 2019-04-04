using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Script = NBitcoin.Script;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    /// <summary>Component that builds an index of all addresses and deposits\withdrawals that happened to\from them.</summary>
    /// <remarks>Disabled by default. Node should be synced from scratch with txindexing enabled to build address index.</remarks>
    public class AddressIndexer : IDisposable
    {
        private readonly ISignals signals;

        private readonly StoreSettings storeSettings;

        private readonly Network network;

        private readonly IBlockStore blockStore;

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        private readonly DataFolder dataFolder;

        private readonly IConsensusManager consensusManager;

        private const string DbKey = "AddrData";

        private SubscriptionToken blockConnectedSubscription, blockDisconnectedSubscription;

        /// <summary>Queue that is populated when block is connected or disconnected.</summary>
        /// <remarks><c>bool</c> key is <c>true</c> when block is connected.</remarks>
        private AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>> blockReceivedQueue;

        private LiteDatabase db;

        private LiteCollection<AddressIndexerData> dataStore;

        private AddressIndexerData addressesIndex;

        public AddressIndexer(StoreSettings storeSettings, ISignals signals, DataFolder dataFolder, ILoggerFactory loggerFactory,
            Network network, IBlockStore blockStore, INodeStats nodeStats, IConsensusManager consensusManager)
        {
            this.signals = signals;
            this.storeSettings = storeSettings;
            this.network = network;
            this.blockStore = blockStore;
            this.nodeStats = nodeStats;
            this.dataFolder = dataFolder;
            this.consensusManager = consensusManager;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (!this.storeSettings.TxIndex || !this.storeSettings.IndexAddresses)
            {
                this.logger.LogTrace("(-)[DISABLED]");
                return;
            }

            string dbPath = Path.Combine(this.dataFolder.RootPath, "addressindex.litedb");
            this.db = new LiteDatabase(new ConnectionString() {Filename = dbPath});

            this.logger.LogDebug("TxIndexing is enabled.");

            this.dataStore = this.db.GetCollection<AddressIndexerData>(DbKey);

            this.addressesIndex = this.dataStore.FindAll().FirstOrDefault();

            if (this.addressesIndex == null)
            {
                this.logger.LogDebug("Tip was not found, initializing with genesis.");

                this.addressesIndex = new AddressIndexerData()
                {
                    TipHash = this.network.GenesisHash.ToString(),
                    AddressIndexDatas = new List<AddressIndexData>()
                };
                this.dataStore.Insert(this.addressesIndex);
            }

            this.blockReceivedQueue = new AsyncQueue<KeyValuePair<bool, ChainedHeaderBlock>>(this.OnEnqueueAsync);

            // Subscribe to events.
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(blockConnectedData =>
            {
                while (this.blockReceivedQueue.Count > 100)
                {
                    this.logger.LogWarning("Address indexing is slowing down the consensus.");
                    Thread.Sleep(5000);
                }

                this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(true, blockConnectedData.ConnectedBlock));
            });

            this.blockDisconnectedSubscription = this.signals.Subscribe<BlockDisconnected>(blockDisconnectedData =>
            {
                this.blockReceivedQueue.Enqueue(new KeyValuePair<bool, ChainedHeaderBlock>(false, blockDisconnectedData.DisconnectedBlock));
            });

            this.nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, 400);

            if ((this.consensusManager.Tip.HashBlock.ToString() != this.addressesIndex.TipHash))
            {
                const string message = "TransactionIndexer is in inconsistent state. This can happen if you've enabled txindex on an already synced or partially synced node. " +
                                       "Remove everything from the data folder and run the node with -txindex=true.";

                this.logger.LogCritical(message);
                this.logger.LogTrace("(-)[INCONSISTENT_STATE]");
                throw new Exception(message);
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

            Block block = item.Value.Block;
            int currentHeight = item.Value.ChainedHeader.Height;

            // Process inputs
            var inputs = new List<TxIn>();

            // Collect all inputs excluding coinbases.
            foreach (TxInList inputsCollection in block.Transactions.Where(x => !x.IsCoinBase).Select(x => x.Inputs))
                inputs.AddRange(inputsCollection);

            Transaction[] transactions = this.blockStore.GetTransactionsByIds(inputs.Select(x => x.PrevOut.Hash).ToArray());

            // TODO is it possible that transactions is null because block with requested ID was reorged away already?

            for (int i = 0; i < inputs.Count; i++)
            {
                TxIn currentInput = inputs[i];

                TxOut txOut = transactions[i].Outputs[currentInput.PrevOut.N];

                Money amountSpent = txOut.Value;

                AddressIndexData addrData = this.GetOrCreateAddressData(txOut.ScriptPubKey);

                if (blockAdded)
                {
                    // Record money being spent.
                    addrData.Changes.Add(new AddressBalanceChange()
                    {
                        BalanceChangedHeight = currentHeight,
                        Satoshi = amountSpent.Satoshi,
                        Deposited = false
                    });
                }
                else
                {
                    // Remove changes.
                    addrData.Changes.RemoveAll(x => x.BalanceChangedHeight == currentHeight);
                }
            }

            // Process outputs.
            foreach (Transaction tx in block.Transactions)
            {
                foreach (TxOut txOut in tx.Outputs)
                {
                    Money amountReceived = txOut.Value;

                    AddressIndexData addrData = this.GetOrCreateAddressData(txOut.ScriptPubKey);

                    if (blockAdded)
                    {
                        // Record money being sent.
                        addrData.Changes.Add(new AddressBalanceChange()
                        {
                            BalanceChangedHeight = currentHeight,
                            Satoshi = amountReceived.Satoshi,
                            Deposited = true
                        });
                    }
                    else
                    {
                        // Remove changes.
                        addrData.Changes.RemoveAll(x => x.BalanceChangedHeight == currentHeight);
                    }
                }
            }

            this.addressesIndex.TipHash = item.Value.ChainedHeader.HashBlock.ToString();
        }

        private AddressIndexData GetOrCreateAddressData(Script scriptPubKey)
        {
            byte[] scriptPubKeyBytes = scriptPubKey.ToBytes();

            AddressIndexData addrData = this.addressesIndex.AddressIndexDatas.SingleOrDefault(x => StructuralComparisons.StructuralEqualityComparer.Equals(scriptPubKeyBytes, x.ScriptPubKeyBytes));

            if (addrData == null)
            {
                addrData = new AddressIndexData()
                {
                    ScriptPubKeyBytes = scriptPubKeyBytes,
                    Changes = new List<AddressBalanceChange>()
                };

                this.addressesIndex.AddressIndexDatas.Add(addrData);
                return addrData;
            }

            return addrData;
        }

        /// <summary>Returns balance of the given address confirmed with at least <paramref name="minConfirmations"/> confirmations.</summary>
        /// <returns>Balance of a given address or <c>null</c> if address wasn't indexed.</returns>
        public Money GetAddressBalance(BitcoinAddress address, int minConfirmations = 0)
        {
            if (this.addressesIndex == null)
                throw new IndexerNotInitializedException();

            throw new NotImplementedException();
        }

        /// <summary>Returns the total amount received by the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        /// <returns>Total amount received by a given address or <c>null</c> if address wasn't indexed.</returns>
        public Money GetReceivedByAddress(BitcoinAddress address, int minConfirmations = 0)
        {
            if (this.addressesIndex == null)
                throw new IndexerNotInitializedException();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.storeSettings.TxIndex && this.storeSettings.IndexAddresses)
            {
                this.signals.Unsubscribe(this.blockConnectedSubscription);
                this.signals.Unsubscribe(this.blockDisconnectedSubscription);

                this.blockReceivedQueue.Dispose();

                this.dataStore.Update(this.addressesIndex);

                this.db.Dispose();
            }
        }
    }

    public class IndexerNotInitializedException : Exception
    {
        public IndexerNotInitializedException() : base("Component wasn't initialized and is not ready to use. Make sure -txindex is set to true.")
        {
        }
    }
}
