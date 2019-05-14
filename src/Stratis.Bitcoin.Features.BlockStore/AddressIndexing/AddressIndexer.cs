﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using FileMode = LiteDB.FileMode;
using Script = NBitcoin.Script;

namespace Stratis.Bitcoin.Features.BlockStore.AddressIndexing
{
    /// <summary>Component that builds an index of all addresses and deposits\withdrawals that happened to\from them.</summary>
    /// <remarks>Disabled by default. Node should be synced from scratch with txindexing enabled to build address index.</remarks>
    public interface IAddressIndexer : IDisposable
    {
        ChainedHeader IndexerTip { get; }

        void Initialize();

        /// <summary>Provides a collection of all indexed addresses and their changes.</summary>
        Dictionary<string, AddressIndexData> GetAddressIndexCopy();

        /// <summary>Returns balance of the given address confirmed with at least <paramref name="minConfirmations"/> confirmations.</summary>
        /// <returns>Balance of a given address or <c>null</c> if address wasn't indexed or doesn't exists.</returns>
        Money GetAddressBalance(string address, int minConfirmations = 0);

        /// <summary>Returns the total amount received by the given address in transactions with at least <paramref name="minConfirmations"/> confirmations.</summary>
        /// <returns>Total amount received by a given address or <c>null</c> if address wasn't indexed.</returns>
        Money GetReceivedByAddress(string address, int minConfirmations = 0);
    }

    public class AddressIndexer : IAddressIndexer
    {
        public ChainedHeader IndexerTip { get; private set; }

        private readonly StoreSettings storeSettings;

        private readonly Network network;

        private readonly IBlockStore blockStore;

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        private readonly DataFolder dataFolder;

        private readonly IConsensusManager consensusManager;

        private readonly IScriptAddressReader scriptAddressReader;

        private readonly TimeSpan flushChangesInterval;

        private const string DbKey = "AddrData";

        private const string DbTipDataKey = "AddrTipData";

        /// <summary>
        /// Time to wait before attempting to index the next block.
        /// Waiting happens after a failure to get next block to index.
        /// </summary>
        private const int DelayTimeMs = 2000;

        private const int BatchSize = 100;

        private LiteDatabase db;

        private LiteCollection<AddressIndexTipData> tipDataStore;

        private LiteCollection<AddressIndexData> dataStore;

        private Dictionary<string, AddressIndexData> addressesIndex;

        private AddressIndexTipData tipData;

        /// <summary>
        /// Only these addresses will be updated while flushing.
        /// </summary>
        private HashSet<string> dirtyAddresses;

        /// <summary>Protects access to <see cref="addressesIndex"/>.</summary>
        private readonly object lockObject;

        private readonly CancellationTokenSource cancellation;

        private Task indexingTask;

        public AddressIndexer(StoreSettings storeSettings, DataFolder dataFolder, ILoggerFactory loggerFactory,
            Network network, IBlockStore blockStore, INodeStats nodeStats, IConsensusManager consensusManager)
        {
            this.storeSettings = storeSettings;
            this.network = network;
            this.blockStore = blockStore;
            this.nodeStats = nodeStats;
            this.dataFolder = dataFolder;
            this.consensusManager = consensusManager;
            this.scriptAddressReader = new ScriptAddressReader();

            this.lockObject = new object();
            this.flushChangesInterval = TimeSpan.FromMinutes(5);
            this.cancellation = new CancellationTokenSource();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (!this.storeSettings.TxIndex || !this.storeSettings.AddressIndex)
            {
                this.logger.LogTrace("(-)[DISABLED]");
                return;
            }

            string dbPath = Path.Combine(this.dataFolder.RootPath, "addressindex.litedb");

            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;
            this.db = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });

            this.logger.LogDebug("TxIndexing is enabled.");

            this.tipDataStore = this.db.GetCollection<AddressIndexTipData>(DbTipDataKey);

            this.dataStore = this.db.GetCollection<AddressIndexData>(DbKey);

            this.addressesIndex = new Dictionary<string, AddressIndexData>();
            this.dirtyAddresses = new HashSet<string>();

            lock (this.lockObject)
            {
                this.tipData = this.tipDataStore.FindAll().FirstOrDefault();

                foreach (AddressIndexData data in this.dataStore.FindAll())
                    this.addressesIndex[data.Address] = data;

                // TODO: Investigate EnsureIndex - can it index block heights to speed up reorg processing?

                if (this.tipData == null)
                {
                    this.logger.LogDebug("Tip was not found, initializing with genesis.");

                    this.tipData = new AddressIndexTipData()
                    {
                        TipHashBytes = this.network.GenesisHash.ToBytes()
                    };
                    this.tipDataStore.Insert(this.tipData);

                    this.addressesIndex.Clear();
                }

                this.IndexerTip = this.consensusManager.Tip.FindAncestorOrSelf(new uint256(this.tipData.TipHashBytes));
            }

            if (this.IndexerTip == null)
                this.IndexerTip = this.consensusManager.Tip.GetAncestor(0);

            this.indexingTask = Task.Run(async () => await this.IndexAddressesContinuouslyAsync().ConfigureAwait(false));

            this.nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 400);
        }

        private async Task IndexAddressesContinuouslyAsync()
        {
            DateTime lastFlushTime = DateTime.Now;
            var batch = new List<AddressIndexData>();

            try
            {
                while (!this.cancellation.IsCancellationRequested)
                {
                    if (DateTime.Now - lastFlushTime > this.flushChangesInterval)
                    {
                        this.logger.LogDebug("Flushing changes.");

                        lock (this.lockObject)
                        {
                            // We maintain a dirty cache so that the whole address index doesn't need to be enumerated every flush.
                            // Benchmarking of LiteDB indicates very strongly that batching has a large performance impact.
                            foreach (string key in this.dirtyAddresses)
                            {
                                batch.Add(this.addressesIndex[key]);

                                if (batch.Count == BatchSize)
                                {
                                    this.dataStore.Update(batch);
                                    batch.Clear();
                                }
                            }

                            if (batch.Count > 0)
                            {
                                this.dataStore.Update(batch);
                                batch.Clear();
                            }

                            this.dirtyAddresses.Clear();
                            this.tipDataStore.Update(this.tipData);
                        }

                        lastFlushTime = DateTime.Now;

                        this.logger.LogDebug("Flush completed.");
                    }

                    if (this.cancellation.IsCancellationRequested)
                        break;

                    ChainedHeader nextHeader = this.consensusManager.Tip.GetAncestor(this.IndexerTip.Height + 1);

                    if (nextHeader == null)
                    {
                        this.logger.LogDebug("Next header wasn't found. Waiting.");

                        try
                        {
                            await Task.Delay(DelayTimeMs, this.cancellation.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        continue;
                    }

                    if (nextHeader.Previous.HashBlock != this.IndexerTip.HashBlock)
                    {
                        ChainedHeader lastCommonHeader = nextHeader.FindFork(this.IndexerTip);

                        this.logger.LogDebug("Reorg detected. Rewinding till '{0}'.", lastCommonHeader);

                        lock (this.lockObject)
                        {
                            foreach (string key in this.addressesIndex.Keys)
                            {
                                // TODO: Possibly introduce concept of 'address index finality' and coalesce records older than maxreorg, so that we don't have to iterate everything
                                if (this.addressesIndex[key].BalanceChanges.RemoveAll(x => x.BalanceChangedHeight > lastCommonHeader.Height) > 0)
                                    this.dirtyAddresses.Add(key);
                            }
                        }

                        this.logger.LogDebug("Reorg processing completed.", lastCommonHeader);

                        this.IndexerTip = lastCommonHeader;
                        continue;
                    }

                    // Get next header block and process it.
                    Block blockToProcess = this.consensusManager.GetBlockData(nextHeader.HashBlock).Block;

                    if (blockToProcess == null)
                    {
                        this.logger.LogDebug("Next block wasn't found. Waiting.");

                        try
                        {
                            await Task.Delay(DelayTimeMs, this.cancellation.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        continue;
                    }

                    bool success = this.ProcessBlock(blockToProcess, nextHeader);

                    if (!success)
                    {
                        this.logger.LogDebug("Failed to process next block. Waiting.");

                        try
                        {
                            await Task.Delay(DelayTimeMs, this.cancellation.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        continue;
                    }

                    this.IndexerTip = nextHeader;

                    lock (this.lockObject)
                    {
                        this.tipData.TipHashBytes = this.IndexerTip.HashBlock.ToBytes();
                    }
                }

                lock (this.lockObject)
                {
                    foreach (string key in this.dirtyAddresses)
                    {
                        batch.Add(this.addressesIndex[key]);

                        if (batch.Count > BatchSize)
                        {
                            this.dataStore.Update(batch);
                            batch.Clear();
                        }
                    }

                    if (batch.Count > 0)
                    {
                        this.dataStore.Update(batch);
                        batch.Clear();
                    }

                    this.dirtyAddresses.Clear();
                    this.tipDataStore.Update(this.tipData);
                }
            }
            catch (Exception e)
            {
                this.logger.LogCritical(e.ToString());
            }
        }

        private void AddInlineStats(StringBuilder benchLog)
        {
            benchLog.AppendLine("AddressIndexer.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                           (this.IndexerTip.Height.ToString().PadRight(8)) +
                           ((" AddressIndexer.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + this.IndexerTip.HashBlock) ));
        }

        /// <summary>Processes block that was added or removed from consensus chain.</summary>
        /// <returns><c>true</c> if block was processed. <c>false</c> if reorg detected and we failed to process a block.</returns>
        private bool ProcessBlock(Block block, ChainedHeader header)
        {
            // Process inputs.
            var inputs = new List<TxIn>();

            // Collect all inputs excluding coinbases.
            foreach (TxInList inputsCollection in block.Transactions.Where(x => !x.IsCoinBase).Select(x => x.Inputs))
                inputs.AddRange(inputsCollection);

            Transaction[] transactions;

            try
            {
                transactions = this.blockStore.GetTransactionsByIds(inputs.Select(x => x.PrevOut.Hash).ToArray(), this.cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELLED]:false");
                return false;
            }

            if ((transactions == null) && (inputs.Count != 0))
            {
                this.logger.LogTrace("(-)[TXES_NOT_FOUND]:false");
                return false;
            }

            lock (this.lockObject)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    TxIn currentInput = inputs[i];

                    TxOut txOut = transactions[i].Outputs[currentInput.PrevOut.N];

                    Money amountSpent = txOut.Value;

                    // Transactions that don't actually change the balance just bloat the database.
                    if (amountSpent == 0)
                        continue;

                    string address = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, txOut.ScriptPubKey);

                    if (string.IsNullOrEmpty(address))
                    {
                        // This condition need not be logged, as the address reader should be aware of all
                        // possible address formats already.
                        continue;
                    }

                    this.dirtyAddresses.Add(address);

                    this.ProcessBalanceChange(header.Height, address, amountSpent, false);
                }

                // Process outputs.
                foreach (Transaction tx in block.Transactions)
                {
                    foreach (TxOut txOut in tx.Outputs)
                    {
                        Money amountReceived = txOut.Value;

                        // Transactions that don't actually change the balance just bloat the database.
                        if (amountReceived == 0)
                            continue;

                        string address = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, txOut.ScriptPubKey);

                        if (string.IsNullOrEmpty(address))
                        {
                            // This condition need not be logged, as the address reader should be aware of all
                            // possible address formats already.
                            continue;
                        }

                        this.dirtyAddresses.Add(address.ToString());

                        this.ProcessBalanceChange(header.Height, address, amountReceived, true);
                    }
                }
            }

            return true;
        }

        private void ProcessBalanceChange(int height, string address, Money amount, bool deposited)
        {
            List<AddressBalanceChange> changes = this.GetOrCreateAddressChangesCollectionLocked(address);

            // Record balance change.
            changes.Add(new AddressBalanceChange()
            {
                BalanceChangedHeight = height,
                Satoshi = amount.Satoshi,
                Deposited = deposited
            });
        }

        /// <remarks>Should be protected by <see cref="lockObject"/>.</remarks>
        private List<AddressBalanceChange> GetOrCreateAddressChangesCollectionLocked(string address)
        {
            if (this.addressesIndex.TryGetValue(address, out AddressIndexData indexData))
            {
                return indexData.BalanceChanges;
            }

            indexData = new AddressIndexData()
            {
                Address = address,
                BalanceChanges = new List<AddressBalanceChange>()
            };

            this.addressesIndex[address] = indexData;
            this.dirtyAddresses.Add(address);

            return indexData.BalanceChanges;
        }

        /// <inheritdoc />
        public Money GetAddressBalance(string address, int minConfirmations = 1)
        {
            if (this.addressesIndex == null)
                throw new IndexerNotInitializedException();

            lock (this.lockObject)
            {
                if (!this.addressesIndex.TryGetValue(address, out AddressIndexData indexData))
                {
                    this.logger.LogTrace("(-)[NOT_FOUND]");
                    return null;
                }

                long balance = 0;

                int maxAllowedHeight = this.consensusManager.Tip.Height - minConfirmations + 1;

                foreach (AddressBalanceChange change in indexData.BalanceChanges.Where(x => x.BalanceChangedHeight <= maxAllowedHeight))
                {
                    if (change.Deposited)
                        balance += change.Satoshi;
                    else
                        balance -= change.Satoshi;
                }

                return new Money(balance);
            }
        }

        /// <inheritdoc />
        public Money GetReceivedByAddress(string address, int minConfirmations = 1)
        {
            if (this.addressesIndex == null)
                throw new IndexerNotInitializedException();

            lock (this.lockObject)
            {
                if (!this.addressesIndex.TryGetValue(address, out AddressIndexData indexData))
                {
                    this.logger.LogTrace("(-)[NOT_FOUND]");
                    return null;
                }

                int maxAllowedHeight = this.consensusManager.Tip.Height - minConfirmations + 1;

                long deposited = indexData.BalanceChanges.Where(x => x.Deposited && x.BalanceChangedHeight <= maxAllowedHeight).Sum(x => x.Satoshi);

                return new Money(deposited);
            }
        }

        /// <inheritdoc/>
        public Dictionary<string, AddressIndexData> GetAddressIndexCopy()
        {
            lock (this.lockObject)
            {
                return new Dictionary<string, AddressIndexData>(this.addressesIndex);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.addressesIndex != null)
            {
                this.cancellation.Cancel();

                this.indexingTask.GetAwaiter().GetResult();

                this.db.Dispose();
            }
        }
    }

    public class IndexerNotInitializedException : Exception
    {
        public IndexerNotInitializedException() : base("Component wasn't initialized and is not ready to use. Make sure -txindex and -addressindex are enabled.")
        {
        }
    }
}
