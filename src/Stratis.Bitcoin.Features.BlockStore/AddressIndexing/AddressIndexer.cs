using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
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
        /// <param name="addresses">The set of addresses that will be queried.</param>
        /// <param name="minConfirmations">Only blocks below consensus tip less this parameter will be considered.</param>
        /// <returns>Balance of a given address or <c>null</c> if address wasn't indexed or doesn't exists.</returns>
        AddressBalancesResult GetAddressBalances(string[] addresses, int minConfirmations = 0);

        /// <summary>Returns verbose balances data.</summary>
        /// <param name="addresses">The set of addresses that will be queried.</param>
        VerboseAddressBalancesResult GetAddressIndexerState(string[] addresses);
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

        private readonly IAsyncProvider asyncProvider;

        private readonly IScriptAddressReader scriptAddressReader;

        private readonly TimeSpan flushChangesInterval;

        private const string DbTipDataKey = "AddrTipData";

        private const string AddressIndexerDatabaseFilename = "addressindex.litedb";

        /// <summary>Max supported reorganization length for networks without max reorg property.</summary>
        public const int FallBackMaxReorg = 200;

        /// <summary>
        /// Time to wait before attempting to index the next block.
        /// Waiting happens after a failure to get next block to index.
        /// </summary>
        private const int DelayTimeMs = 2000;

        private const int CompactingThreshold = 50;

        /// <summary>Max distance between consensus and indexer tip to consider indexer synced.</summary>
        private const int ConsiderSyncedMaxDistance = 10;

        private LiteDatabase db;

        private LiteCollection<AddressIndexerTipData> tipDataStore;

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

        private readonly ChainIndexer chainIndexer;

        private readonly AverageCalculator averageTimePerBlock;

        private readonly IDateTimeProvider dateTimeProvider;

        private Task indexingTask;

        private DateTime lastFlushTime;

        private const int PurgeIntervalSeconds = 60;

        /// <summary>Last time rewind data was purged.</summary>
        private DateTime lastPurgeTime;

        private Task<ChainedHeaderBlock> prefetchingTask;

        /// <summary>Indexer height at the last save.</summary>
        /// <remarks>Should be protected by <see cref="lockObject"/>.</remarks>
        private int lastSavedHeight;

        /// <summary>Distance in blocks from consensus tip at which compaction should start.</summary>
        /// <remarks>It can't be lower than maxReorg since compacted data can't be converted back to uncompacted state for partial reversion.</remarks>
        private readonly int compactionTriggerDistance;

        /// <summary>
        /// This is a window of some blocks that is needed to reduce the consequences of nodes having different view of consensus chain.
        /// We assume that nodes usually don't have view that is different from other nodes by that constant of blocks.
        /// </summary>
        public const int SyncBuffer = 50;

        public AddressIndexer(StoreSettings storeSettings, DataFolder dataFolder, ILoggerFactory loggerFactory, Network network, INodeStats nodeStats,
            IConsensusManager consensusManager, IAsyncProvider asyncProvider, ChainIndexer chainIndexer, IDateTimeProvider dateTimeProvider)
        {
            this.storeSettings = storeSettings;
            this.network = network;
            this.nodeStats = nodeStats;
            this.dataFolder = dataFolder;
            this.consensusManager = consensusManager;
            this.asyncProvider = asyncProvider;
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.scriptAddressReader = new ScriptAddressReader();

            this.lockObject = new object();
            this.flushChangesInterval = TimeSpan.FromMinutes(2);
            this.lastFlushTime = this.dateTimeProvider.GetUtcNow();
            this.cancellation = new CancellationTokenSource();
            this.chainIndexer = chainIndexer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.averageTimePerBlock = new AverageCalculator(200);
            int maxReorgLength = GetMaxReorgOrFallbackMaxReorg(this.network);

            this.compactionTriggerDistance = maxReorgLength * 2 + SyncBuffer + 1000;
        }

        /// <summary>Returns maxReorg of <see cref="FallBackMaxReorg"/> in case maxReorg is <c>0</c>.</summary>
        public static int GetMaxReorgOrFallbackMaxReorg(Network network)
        {
            int maxReorgLength = network.Consensus.MaxReorgLength == 0 ? FallBackMaxReorg : (int)network.Consensus.MaxReorgLength;

            return maxReorgLength;
        }

        public void Initialize()
        {
            // The transaction index is needed in the event of a reorg.
            if (!this.storeSettings.AddressIndex)
            {
                this.logger.LogTrace("(-)[DISABLED]");
                return;
            }

            string dbPath = Path.Combine(this.dataFolder.RootPath, AddressIndexerDatabaseFilename);

            FileMode fileMode = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? FileMode.Exclusive : FileMode.Shared;
            this.db = new LiteDatabase(new ConnectionString() { Filename = dbPath, Mode = fileMode });

            this.addressIndexRepository = new AddressIndexRepository(this.db, this.loggerFactory);

            this.logger.LogDebug("Address indexing is enabled.");

            this.tipDataStore = this.db.GetCollection<AddressIndexerTipData>(DbTipDataKey);

            lock (this.lockObject)
            {
                AddressIndexerTipData tipData = this.tipDataStore.FindAll().FirstOrDefault();

                this.logger.LogDebug("Tip data: '{0}'.", tipData == null ? "null" : tipData.ToString());

                this.IndexerTip = tipData == null ? this.chainIndexer.Genesis : this.consensusManager.Tip.FindAncestorOrSelf(new uint256(tipData.TipHashBytes));

                if (this.IndexerTip == null)
                {
                    // This can happen if block hash from tip data is no longer a part of the consensus chain and node was killed in the middle of a reorg.
                    int rewindAmount = this.compactionTriggerDistance / 2;

                    if (rewindAmount > this.consensusManager.Tip.Height)
                        this.IndexerTip = this.chainIndexer.Genesis;
                    else
                        this.IndexerTip = this.consensusManager.Tip.GetAncestor(this.consensusManager.Tip.Height - rewindAmount);
                }
            }

            this.outpointsRepository = new AddressIndexerOutpointsRepository(this.db, this.loggerFactory);

            this.RewindAndSave(this.IndexerTip);

            this.logger.LogDebug("Indexer initialized at '{0}'.", this.IndexerTip);

            this.indexingTask = Task.Run(async () => await this.IndexAddressesContinuouslyAsync().ConfigureAwait(false));

            this.asyncProvider.RegisterTask($"{nameof(AddressIndexer)}.{nameof(this.indexingTask)}", this.indexingTask);

            this.nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, this.GetType().Name, 400);
        }

        private async Task IndexAddressesContinuouslyAsync()
        {
            var watch = Stopwatch.StartNew();

            while (!this.cancellation.IsCancellationRequested)
            {
                if (this.dateTimeProvider.GetUtcNow() - this.lastFlushTime > this.flushChangesInterval)
                {
                    this.logger.LogDebug("Flushing changes.");

                    this.SaveAll();

                    this.lastFlushTime = this.dateTimeProvider.GetUtcNow();

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

                    this.RewindAndSave(lastCommonHeader);

                    continue;
                }

                // First try to see if it's prefetched.
                ChainedHeaderBlock prefetchedBlock = this.prefetchingTask == null ? null : await this.prefetchingTask.ConfigureAwait(false);

                Block blockToProcess;

                if (prefetchedBlock != null && prefetchedBlock.ChainedHeader == nextHeader)
                    blockToProcess = prefetchedBlock.Block;
                else
                    blockToProcess = this.consensusManager.GetBlockData(nextHeader.HashBlock).Block;

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

                // Schedule prefetching of the next block;
                ChainedHeader headerToPrefetch = this.consensusManager.Tip.GetAncestor(nextHeader.Height + 1);

                if (headerToPrefetch != null)
                    this.prefetchingTask = Task.Run(() => this.consensusManager.GetBlockData(headerToPrefetch.HashBlock));

                watch.Restart();

                bool success = this.ProcessBlock(blockToProcess, nextHeader);

                watch.Stop();
                this.averageTimePerBlock.AddSample(watch.Elapsed.TotalMilliseconds);

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
            }

            this.SaveAll();
        }

        private void RewindAndSave(ChainedHeader rewindToHeader)
        {
            lock (this.lockObject)
            {
                // The cache doesn't really lend itself to handling a reorg very well.
                // Therefore, we leverage LiteDb's indexing capabilities to tell us
                // which records are for the affected blocks.

                List<string> affectedAddresses = this.addressIndexRepository.GetAddressesHigherThanHeight(rewindToHeader.Height);

                foreach (string address in affectedAddresses)
                {
                    AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);
                    indexData.BalanceChanges.RemoveAll(x => x.BalanceChangedHeight > rewindToHeader.Height);
                }

                this.logger.LogDebug("Rewinding changes for {0} addresses.", affectedAddresses.Count);

                // Rewind all the way back to the fork point.
                this.outpointsRepository.RewindDataAboveHeight(rewindToHeader.Height);

                this.IndexerTip = rewindToHeader;

                this.SaveAll();
            }
        }

        private void SaveAll()
        {
            this.logger.LogDebug("Saving address indexer.");

            lock (this.lockObject)
            {
                this.addressIndexRepository.SaveAllItems();
                this.outpointsRepository.SaveAllItems();

                AddressIndexerTipData tipData = this.tipDataStore.FindAll().FirstOrDefault();

                if (tipData == null)
                    tipData = new AddressIndexerTipData();

                tipData.Height = this.IndexerTip.Height;
                tipData.TipHashBytes = this.IndexerTip.HashBlock.ToBytes();

                this.tipDataStore.Upsert(tipData);
                this.lastSavedHeight = this.IndexerTip.Height;
            }

            this.logger.LogDebug("Address indexer saved.");
        }

        private void AddInlineStats(StringBuilder benchLog)
        {
            benchLog.AppendLine("AddressIndexer.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + this.IndexerTip.Height.ToString().PadRight(9) +
                                "AddressCache%: " + this.addressIndexRepository.GetLoadPercentage().ToString().PadRight(8) +
                                "OutPointCache%: " + this.outpointsRepository.GetLoadPercentage().ToString().PadRight(8) +
                                $"Ms/block: {Math.Round(this.averageTimePerBlock.Average, 2)}");
        }

        /// <summary>Processes a block that was added or removed from the consensus chain.</summary>
        /// <param name="block">The block to process.</param>
        /// <param name="header">The chained header associated to the block being processed.</param>
        /// <returns><c>true</c> if block was sucessfully processed.</returns>
        private bool ProcessBlock(Block block, ChainedHeader header)
        {
            lock (this.lockObject)
            {
                // Record outpoints.
                foreach (Transaction tx in block.Transactions)
                {
                    for (int i = 0; i < tx.Outputs.Count; i++)
                    {
                        // OP_RETURN outputs and empty outputs cannot be spent and therefore do not need to be put into the cache.
                        if (tx.Outputs[i].IsEmpty || tx.Outputs[i].ScriptPubKey.IsUnspendable)
                            continue;

                        var outPoint = new OutPoint(tx, i);

                        var outPointData = new OutPointData()
                        {
                            Outpoint = outPoint.ToString(),
                            ScriptPubKeyBytes = tx.Outputs[i].ScriptPubKey.ToBytes(),
                            Money = tx.Outputs[i].Value
                        };

                        // TODO: When the outpoint cache is full, adding outpoints singly causes overhead writing evicted entries out to the repository
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
                var rewindData = new AddressIndexerRewindData() { BlockHash = header.HashBlock.ToString(), BlockHeight = header.Height, SpentOutputs = new List<OutPointData>() };

                foreach (TxIn input in inputs)
                {
                    OutPoint consumedOutput = input.PrevOut;

                    if (!this.outpointsRepository.TryGetOutPointData(consumedOutput, out OutPointData consumedOutputData))
                    {
                        this.logger.LogError("Missing outpoint data for {0}.", consumedOutput);
                        this.logger.LogTrace("(-)[MISSING_OUTPOINTS_DATA]");
                        throw new Exception($"Missing outpoint data for {consumedOutput}");
                    }

                    Money amountSpent = consumedOutputData.Money;

                    rewindData.SpentOutputs.Add(consumedOutputData);

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
                        if (amountReceived == 0 || txOut.IsEmpty || txOut.ScriptPubKey.IsUnspendable)
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

                this.outpointsRepository.RecordRewindData(rewindData);

                int purgeRewindDataThreshold = Math.Min(this.consensusManager.Tip.Height - this.compactionTriggerDistance, this.lastSavedHeight);

                if ((this.dateTimeProvider.GetUtcNow() - this.lastPurgeTime).TotalSeconds > PurgeIntervalSeconds)
                {
                    this.outpointsRepository.PurgeOldRewindData(purgeRewindDataThreshold);
                    this.lastPurgeTime = this.dateTimeProvider.GetUtcNow();
                }

                // Remove outpoints that were consumed.
                foreach (OutPoint consumedOutPoint in inputs.Select(x => x.PrevOut))
                    this.outpointsRepository.RemoveOutPointData(consumedOutPoint);
            }

            return true;
        }

        /// <summary>Adds a new balance change entry to to the <see cref="addressIndexRepository"/>.</summary>
        /// <param name="height">The height of the block this being processed.</param>
        /// <param name="address">The address receiving the funds.</param>
        /// <param name="amount">The amount being received.</param>
        /// <param name="deposited"><c>false</c> if this is an output being spent, <c>true</c> otherwise.</param>
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

            // Anything less than that should be compacted.
            int heightThreshold = this.consensusManager.Tip.Height - this.compactionTriggerDistance;

            bool compact = (indexData.BalanceChanges.Count > CompactingThreshold) &&
                           (indexData.BalanceChanges[1].BalanceChangedHeight < heightThreshold);

            if (!compact)
            {
                this.logger.LogTrace("(-)[TOO_FEW_CHANGE_RECORDS]");
                return;
            }

            var compacted = new List<AddressBalanceChange>(CompactingThreshold / 2)
            {
                new AddressBalanceChange()
                {
                    BalanceChangedHeight = 0,
                    Satoshi = 0,
                    Deposited = true
                }
            };

            foreach (AddressBalanceChange change in indexData.BalanceChanges)
            {
                if (change.BalanceChangedHeight < heightThreshold)
                {
                    this.logger.LogDebug("Balance change: {0} was selected for compaction. Compacted balance now: {1}.", change, compacted[0].Satoshi);

                    if (change.Deposited)
                        compacted[0].Satoshi += change.Satoshi;
                    else
                        compacted[0].Satoshi -= change.Satoshi;

                    this.logger.LogDebug("New compacted balance: {0}.", compacted[0].Satoshi);
                }
                else
                    compacted.Add(change);
            }

            indexData.BalanceChanges = compacted;
            this.addressIndexRepository.AddOrUpdate(indexData.Address, indexData, indexData.BalanceChanges.Count + 1);
        }

        private bool IsSynced()
        {
            lock (this.lockObject)
            {
                return this.consensusManager.Tip.Height - this.IndexerTip.Height <= ConsiderSyncedMaxDistance;
            }
        }

        /// <inheritdoc />
        /// <remarks>This is currently not in use but will be required for exchange integration.</remarks>
        public AddressBalancesResult GetAddressBalances(string[] addresses, int minConfirmations = 1)
        {
            var (isQueryable, reason) = this.IsQueryable();

            if (!isQueryable)
                return AddressBalancesResult.RequestFailed(reason);

            var result = new AddressBalancesResult();

            lock (this.lockObject)
            {
                foreach (var address in addresses)
                {
                    AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);

                    int maxAllowedHeight = this.consensusManager.Tip.Height - minConfirmations + 1;

                    long balance = indexData.BalanceChanges.Where(x => x.BalanceChangedHeight <= maxAllowedHeight).CalculateBalance();

                    this.logger.LogDebug("Address: {0}, balance: {1}.", address, balance);
                    result.Balances.Add(new AddressBalanceResult(address, new Money(balance)));
                }

                return result;
            }
        }

        /// <inheritdoc />
        public VerboseAddressBalancesResult GetAddressIndexerState(string[] addresses)
        {
            var result = new VerboseAddressBalancesResult(this.consensusManager.Tip.Height);

            if (addresses.Length == 0)
                return result;

            (bool isQueryable, string reason) = this.IsQueryable();

            if (!isQueryable)
                return VerboseAddressBalancesResult.RequestFailed(reason);

            lock (this.lockObject)
            {
                foreach (var address in addresses)
                {
                    AddressIndexerData indexData = this.addressIndexRepository.GetOrCreateAddress(address);

                    var copy = new AddressIndexerData()
                    {
                        Address = indexData.Address,
                        BalanceChanges = new List<AddressBalanceChange>(indexData.BalanceChanges)
                    };

                    result.BalancesData.Add(copy);
                }
            }

            return result;
        }

        private (bool isQueryable, string reason) IsQueryable()
        {
            if (this.addressIndexRepository == null)
            {
                this.logger.LogTrace("(-)[NOT_INITIALIZED]");
                return (false, "Address indexer is not initialized.");
            }

            if (!this.IsSynced())
            {
                this.logger.LogTrace("(-)[NOT_SYNCED]");
                return (false, "Address indexer is not synced.");
            }

            return (true, string.Empty);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.cancellation.Cancel();

            this.indexingTask?.GetAwaiter().GetResult();

            this.db?.Dispose();
        }
    }
}
