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
    /// <remarks>Disabled by default. Node should be synced from scratch with txindexing enabled to build address index.</remarks>
    public interface IAddressIndexer : IDisposable
    {
        ChainedHeader IndexerTip { get; }

        void Initialize();

        /// <summary>Provides a collection of all indexed addresses and their changes.</summary>
        Dictionary<string, List<AddressBalanceChange>> GetAddressIndexCopy();

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

        private readonly TimeSpan flushChangesInterval;

        private const string DbKey = "AddrData";

        /// <summary>
        /// Time to wait before attempting to index the next block.
        /// Waiting happens after a failure to get next block to index.
        /// </summary>
        private const int DelayTimeMs = 2000;

        private LiteDatabase db;

        private LiteCollection<AddressIndexerData> dataStore;

        private AddressIndexerData addressesIndex;

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
            this.db = new LiteDatabase(new ConnectionString() {Filename = dbPath, Mode = fileMode });

            this.logger.LogDebug("TxIndexing is enabled.");

            this.dataStore = this.db.GetCollection<AddressIndexerData>(DbKey);

            lock (this.lockObject)
            {
                this.addressesIndex = this.dataStore.FindAll().FirstOrDefault();

                if (this.addressesIndex == null)
                {
                    this.logger.LogDebug("Tip was not found, initializing with genesis.");

                    this.addressesIndex = new AddressIndexerData()
                    {
                        TipHashBytes = this.network.GenesisHash.ToBytes(),
                        AddressChanges = new Dictionary<string, List<AddressBalanceChange>>()
                    };
                    this.dataStore.Insert(this.addressesIndex);
                }

                this.IndexerTip = this.consensusManager.Tip.FindAncestorOrSelf(new uint256(this.addressesIndex.TipHashBytes));
            }

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
                            this.dataStore.Update(this.addressesIndex);
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
                            foreach (List<AddressBalanceChange> changes in this.addressesIndex.AddressChanges.Values)
                                changes.RemoveAll(x => x.BalanceChangedHeight > lastCommonHeader.Height);
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
                        this.addressesIndex.TipHashBytes = this.IndexerTip.HashBlock.ToBytes();
                    }
                }

                lock (this.lockObject)
                {
                    this.dataStore.Update(this.addressesIndex);
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
            // Process inputs
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

                    BitcoinAddress address = this.GetAddressFromScriptPubKey(txOut.ScriptPubKey);

                    if (address == null)
                    {
                        this.logger.LogDebug("Failed to extract an address from '{0}' while parsing inputs.", txOut.ScriptPubKey);
                        continue;
                    }

                    List<AddressBalanceChange> changes = this.GetOrCreateAddressChangesCollectionLocked(address.ToString());

                    // Record money being spent.
                    changes.Add(new AddressBalanceChange()
                    {
                        BalanceChangedHeight = header.Height,
                        Satoshi = amountSpent.Satoshi,
                        Deposited = false
                    });
                }

                // Process outputs.
                foreach (Transaction tx in block.Transactions)
                {
                    foreach (TxOut txOut in tx.Outputs)
                    {
                        Money amountReceived = txOut.Value;

                        BitcoinAddress address = this.GetAddressFromScriptPubKey(txOut.ScriptPubKey);

                        if (address == null)
                        {
                            this.logger.LogDebug("Failed to extract an address from '{0}' while parsing outputs.", txOut.ScriptPubKey);
                            continue;
                        }

                        List<AddressBalanceChange> changes = this.GetOrCreateAddressChangesCollectionLocked(address.ToString());

                        // Record money being sent.
                        changes.Add(new AddressBalanceChange()
                        {
                            BalanceChangedHeight = header.Height,
                            Satoshi = amountReceived.Satoshi,
                            Deposited = true
                        });
                    }
                }
            }

            return true;
        }

        private BitcoinAddress GetAddressFromScriptPubKey(Script scriptPubKey)
        {
            BitcoinAddress address = scriptPubKey.GetDestinationAddress(this.network);

            if (address == null)
            {
                // Handle P2PK
                PubKey[] destinationKeys = scriptPubKey.GetDestinationPublicKeys(this.network);

                if (destinationKeys.Length == 1)
                    address = destinationKeys[0].GetAddress(this.network);
            }

            return address;
        }

        /// <remarks>Should be protected by <see cref="lockObject"/>.</remarks>
        private List<AddressBalanceChange> GetOrCreateAddressChangesCollectionLocked(string address)
        {
            if (this.addressesIndex.AddressChanges.TryGetValue(address, out List<AddressBalanceChange> changes))
            {
                return changes;
            }

            changes = new List<AddressBalanceChange>();
            this.addressesIndex.AddressChanges[address] = changes;

            return changes;
        }

        /// <inheritdoc />
        public Money GetAddressBalance(string address, int minConfirmations = 1)
        {
            if (this.addressesIndex == null)
                throw new IndexerNotInitializedException();

            lock (this.lockObject)
            {
                if (!this.addressesIndex.AddressChanges.TryGetValue(address, out List<AddressBalanceChange> changes))
                {
                    this.logger.LogTrace("(-)[NOT_FOUND]");
                    return null;
                }

                long balance = 0;

                int maxAllowedHeight = this.consensusManager.Tip.Height - minConfirmations + 1;

                foreach (AddressBalanceChange change in changes.Where(x => x.BalanceChangedHeight <= maxAllowedHeight))
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
                if (!this.addressesIndex.AddressChanges.TryGetValue(address, out List<AddressBalanceChange> changes))
                {
                    this.logger.LogTrace("(-)[NOT_FOUND]");
                    return null;
                }

                int maxAllowedHeight = this.consensusManager.Tip.Height - minConfirmations + 1;

                long deposited = changes.Where(x => x.Deposited && x.BalanceChangedHeight <= maxAllowedHeight).Sum(x => x.Satoshi);

                return new Money(deposited);
            }
        }

        /// <inheritdoc/>
        public Dictionary<string, List<AddressBalanceChange>> GetAddressIndexCopy()
        {
            lock (this.lockObject)
            {
                return new Dictionary<string, List<AddressBalanceChange>>(this.addressesIndex.AddressChanges);
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
        public IndexerNotInitializedException() : base("Component wasn't initialized and is not ready to use. Make sure -txindex is set to true.")
        {
        }
    }
}
