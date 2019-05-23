using System;
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
    public interface IAddressIndexer : IDisposable
    {
        ChainedHeader IndexerTip { get; }

        void Initialize();

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

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        private readonly DataFolder dataFolder;

        private readonly IConsensusManager consensusManager;

        private readonly IScriptAddressReader scriptAddressReader;

        private readonly TimeSpan flushChangesInterval;

        private const string DbTipDataKey = "AddrTipData";

        private const string AddressIndexerDatabaseFilename = "addressindex.litedb";

        /// <summary>
        /// Time to wait before attempting to index the next block.
        /// Waiting happens after a failure to get next block to index.
        /// </summary>
        private const int DelayTimeMs = 2000;

        private LiteDatabase db;

        private LiteCollection<AddressIndexerTipData> tipDataStore;

        private AddressIndexerTipData tipData;

        /// <summary>A mapping between addresses and their balance changes.</summary>
        /// <remarks>All access should be protected by <see cref="lockObject"/>.</remarks>
        private AddressIndexRepository addressIndexRepository;

        /// <summary>Script pub keys and amounts mapped by outpoints.</summary>
        /// <remarks>All access should be protected by <see cref="lockObject"/>.</remarks>
        private AddressIndexerOutpointsRepository outpointsRepository;

        /// <summary>Protects access to <see cref="addressIndexRepository"/> and <see cref="outpointsRepository"/>.</summary>
        private readonly object lockObject;

        private readonly CancellationTokenSource cancellation;

        private readonly ILoggerFactory loggerFactory;

        private Task indexingTask;

        public AddressIndexer(StoreSettings storeSettings, DataFolder dataFolder, ILoggerFactory loggerFactory, Network network, INodeStats nodeStats, IConsensusManager consensusManager)
        {
            this.storeSettings = storeSettings;
            this.network = network;
            this.nodeStats = nodeStats;
            this.dataFolder = dataFolder;
            this.consensusManager = consensusManager;
            this.loggerFactory = loggerFactory;
            this.scriptAddressReader = new ScriptAddressReader();

            this.lockObject = new object();
            this.flushChangesInterval = TimeSpan.FromMinutes(10);
            this.cancellation = new CancellationTokenSource();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            if (!this.storeSettings.AddressIndex)
            {
                this.logger.LogTrace("(-)[DISABLED]");
                return;
            }

            string dbPath = Path.Combine(this.dataFolder.RootPath, AddressIndexerDatabaseFilename);

            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;
            this.db = new LiteDatabase(new ConnectionString() {Filename = dbPath, Mode = fileMode });

            this.addressIndexRepository = new AddressIndexRepository(this.db, this.loggerFactory);

            this.logger.LogDebug("Address indexing is enabled.");

            this.tipDataStore = this.db.GetCollection<AddressIndexerTipData>(DbTipDataKey);

            lock (this.lockObject)
            {
                this.tipData = this.tipDataStore.FindAll().FirstOrDefault();

                if (this.tipData == null)
                {
                    this.logger.LogDebug("Tip was not found, initializing with genesis.");

                    this.tipData = new AddressIndexerTipData()
                    {
                        TipHashBytes = this.network.GenesisHash.ToBytes()
                    };

                    this.tipDataStore.Insert(this.tipData);
                }

                this.IndexerTip = this.consensusManager.Tip.FindAncestorOrSelf(new uint256(this.tipData.TipHashBytes));
            }

            this.outpointsRepository = new AddressIndexerOutpointsRepository(this.db, this.loggerFactory);

            if (this.IndexerTip == null)
                this.IndexerTip = this.consensusManager.Tip.GetAncestor(0);

            this.indexingTask = Task.Run(async () => await this.IndexAddressesContinuouslyAsync().ConfigureAwait(false));

            this.nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 400);
        }

        private async Task IndexAddressesContinuouslyAsync()
        {
            DateTime lastFlushTime = DateTime.Now;

            try
            {
                while (!this.cancellation.IsCancellationRequested)
                {
                    if (DateTime.Now - lastFlushTime > this.flushChangesInterval)
                    {
                        this.logger.LogDebug("Flushing changes.");

                        lock (this.lockObject)
                        {
                            this.addressIndexRepository.SaveAllItems();
                            this.outpointsRepository.SaveAllItems();
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

                        this.logger.LogDebug("Reorganization detected. Rewinding till '{0}'.", lastCommonHeader);

                        lock (this.lockObject)
                        {
                            // The cache doesn't really lend itself to handling a reorg very well.
                            // Therefore, we leverage LiteDb's indexing capabilities to tell us
                            // which records are for the affected blocks.
                            // TODO: May also be efficient to run ProcessBlocks with inverted deposit flags instead, depending on size of reorg

                            List<string> affectedAddresses = this.addressIndexRepository.GetAddressesHigherThanHeight(lastCommonHeader.Height);

                            foreach (string address in affectedAddresses)
                            {
                                AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);
                                indexData.BalanceChanges.RemoveAll(x => x.BalanceChangedHeight > lastCommonHeader.Height);
                            }
                        }

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
                    this.addressIndexRepository.SaveAllItems();
                    this.outpointsRepository.SaveAllItems();
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
        /// <returns><c>true</c> if block was processed.</returns>
        private bool ProcessBlock(Block block, ChainedHeader header)
        {
            lock (this.lockObject)
            {
                // Record outpoints.
                foreach (Transaction tx in block.Transactions)
                {
                    for (int i = 0; i < tx.Outputs.Count; i++)
                    {
                        if (tx.Outputs[i].Value == Money.Zero)
                            continue;

                        var outPoint = new OutPoint(tx, i);

                        var outPointData = new OutPointData()
                        {
                            Outpoint = outPoint.ToString(),
                            ScriptPubKeyBytes = tx.Outputs[i].ScriptPubKey.ToBytes(),
                            Money = tx.Outputs[i].Value
                        };

                        this.outpointsRepository.AddOutPointData(outPointData);
                    }
                }
            }

            // Process inputs.
            var inputs = new List<TxIn>();

            // Collect all inputs excluding coinbases.
            foreach (TxInList inputsCollection in block.Transactions.Where(x => !x.IsCoinBase).Select(x => x.Inputs))
                inputs.AddRange(inputsCollection);

            lock (this.lockObject)
            {
                foreach (TxIn input in inputs)
                {
                    OutPoint consumedOutput = input.PrevOut;

                    if (!this.outpointsRepository.TryGetOutPointData(consumedOutput, out OutPointData consumedOutputData))
                        throw new Exception($"Missing outpoint data for {consumedOutput}");

                    Money amountSpent = consumedOutputData.Money;

                    this.outpointsRepository.RemoveOutPointData(consumedOutput);

                    // Transactions that don't actually change the balance just bloat the database.
                    if (amountSpent == 0)
                        continue;

                    string address = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, new Script(consumedOutputData.ScriptPubKeyBytes));

                    if (string.IsNullOrEmpty(address))
                    {
                        // This condition need not be logged, as the address reader should be aware of all possible address formats already.
                        continue;
                    }

                    this.ProcessBalanceChangeLocked(header.Height, address, amountSpent, false);
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

                        this.ProcessBalanceChangeLocked(header.Height, address, amountReceived, true);
                    }
                }
            }

            return true;
        }

        /// <summary>Adds a new balance change entry to to the <see cref="addressIndexRepository"/>.</summary>
        /// <remarks>Should be protected by <see cref="lockObject"/>.</remarks>
        private void ProcessBalanceChangeLocked(int height, string address, Money amount, bool deposited)
        {
            AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);

            // Record new balance change into the address index data.
            indexData.BalanceChanges.Add(new AddressBalanceChange()
            {
                BalanceChangedHeight = height,
                Satoshi = amount.Satoshi,
                Deposited = deposited
            });

            // As an approximate heuristic, there will generally only be one balance change per block.
            // Therefore we decide whether to trigger compaction or not based on the number of balance
            // changes. This is not a perfect approach, but enumerating the balance change list
            // unnecessarily could slow down indexing performance substantially.
            if ((this.network.Consensus.MaxReorgLength <= 0) || (indexData.BalanceChanges.Count <= this.network.Consensus.MaxReorgLength))
            {
                this.logger.LogTrace("(-)[TOO_FEW_CHANGE_RECORDS]");
                return;
            }

            // The rebuilt balance change list.
            var compacted = new List<AddressBalanceChange>();

            // A standalone record for holding all the compacted changes.
            var compactedRecord = new AddressBalanceChange()
            {
                BalanceChangedHeight = 0,
                Satoshi = 0,
                Deposited = true
            };

            int threshold = this.consensusManager.Tip.Height - (int)this.network.Consensus.MaxReorgLength;
            bool updated = false;
            foreach (AddressBalanceChange change in indexData.BalanceChanges)
            {
                if (change.BalanceChangedHeight < threshold)
                {
                    updated = true;

                    if (change.Deposited)
                        compactedRecord.Satoshi += change.Satoshi;
                    else
                        compactedRecord.Satoshi -= change.Satoshi;
                }
                else
                {
                    compacted.Add(change);
                }
            }

            if (!updated)
            {
                this.logger.LogTrace("(-)[NO_UPDATES]");
                return;
            }

            compacted.Add(compactedRecord);
            indexData.BalanceChanges = compacted;
            this.addressIndexRepository.AddOrUpdate(indexData.Address, indexData, indexData.BalanceChanges.Count + 1);
        }

        /// <inheritdoc />
        public Money GetAddressBalance(string address, int minConfirmations = 1)
        {
            if (this.addressIndexRepository == null)
                throw new IndexerNotInitializedException();

            lock (this.lockObject)
            {
                AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);
                if (indexData == null)
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
            if (this.addressIndexRepository == null)
                throw new IndexerNotInitializedException();

            lock (this.lockObject)
            {
                AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);
                if (indexData == null)
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
        public void Dispose()
        {
            this.cancellation.Cancel();

            this.indexingTask?.GetAwaiter().GetResult();

            this.db?.Dispose();
        }
    }

    public class IndexerNotInitializedException : Exception
    {
        public IndexerNotInitializedException() : base("Component wasn't initialized and is not ready to use.")
        {
        }
    }
}
