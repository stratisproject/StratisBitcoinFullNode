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
        private readonly StoreSettings storeSettings;

        private readonly Network network;

        private readonly IBlockStore blockStore;

        private readonly INodeStats nodeStats;

        private readonly ILogger logger;

        private readonly DataFolder dataFolder;

        private readonly IConsensusManager consensusManager;

        private readonly TimeSpan flushChangesInterval;

        private const string DbKey = "AddrData";

        private LiteDatabase db;

        private LiteCollection<AddressIndexerData> dataStore;

        private AddressIndexerData addressesIndex;

        private CancellationTokenSource calcellation;

        private Task indexingTask;

        private ChainedHeader chainedHeaderTip;

        public AddressIndexer(StoreSettings storeSettings, DataFolder dataFolder, ILoggerFactory loggerFactory,
            Network network, IBlockStore blockStore, INodeStats nodeStats, IConsensusManager consensusManager)
        {
            this.storeSettings = storeSettings;
            this.network = network;
            this.blockStore = blockStore;
            this.nodeStats = nodeStats;
            this.dataFolder = dataFolder;
            this.consensusManager = consensusManager;

            this.flushChangesInterval = TimeSpan.FromMinutes(5);
            this.calcellation = new CancellationTokenSource();
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
            this.db = new LiteDatabase(new ConnectionString() {Filename = dbPath});

            this.logger.LogDebug("TxIndexing is enabled.");

            this.dataStore = this.db.GetCollection<AddressIndexerData>(DbKey);

            this.addressesIndex = this.dataStore.FindAll().FirstOrDefault();

            if (this.addressesIndex == null)
            {
                this.logger.LogDebug("Tip was not found, initializing with genesis.");

                this.addressesIndex = new AddressIndexerData()
                {
                    TipHashBytes = this.network.GenesisHash.ToBytes(),
                    AddressIndexDatas = new List<AddressIndexData>()
                };
                this.dataStore.Insert(this.addressesIndex);
            }

            this.chainedHeaderTip = this.consensusManager.Tip.FindAncestorOrSelf(new uint256(this.addressesIndex.TipHashBytes));

            if (this.chainedHeaderTip == null)
                this.chainedHeaderTip = this.consensusManager.Tip.GetAncestor(0);

            this.indexingTask = this.IndexAddressesContinuouslyAsync();

            this.nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, 400);
        }

        private async Task IndexAddressesContinuouslyAsync()
        {
            bool triggerFlush = false;
            DateTime lastFlushTime = DateTime.Now;

            try
            {
                while (!this.calcellation.IsCancellationRequested)
                {
                    if (triggerFlush || (DateTime.Now - lastFlushTime > this.flushChangesInterval))
                    {
                        this.logger.LogDebug("Flushing changes.");

                        this.dataStore.Update(this.addressesIndex);
                        triggerFlush = false;
                        lastFlushTime = DateTime.Now;

                        this.logger.LogDebug("Flush completed.");
                    }

                    ChainedHeader nextHeader = this.consensusManager.Tip.GetAncestor(this.chainedHeaderTip.Height + 1);

                    if (nextHeader == null)
                    {
                        triggerFlush = true;

                        try
                        {
                            await Task.Delay(2_000, this.calcellation.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        continue;
                    }

                    if (nextHeader.Previous.HashBlock != this.chainedHeaderTip.HashBlock)
                    {
                        ChainedHeader lastCommonHeader = nextHeader.FindFork(this.chainedHeaderTip);

                        this.logger.LogDebug("Reorg detected. Rewinding till '{0}'.", lastCommonHeader);

                        foreach (AddressIndexData addressIndexData in this.addressesIndex.AddressIndexDatas)
                            addressIndexData.Changes.RemoveAll(x => x.BalanceChangedHeight > lastCommonHeader.Height);

                        this.chainedHeaderTip = lastCommonHeader;
                        continue;
                    }

                    // Get next header block and process it.
                    Block blockToProcess = this.blockStore.GetBlock(nextHeader.HashBlock);

                    if (blockToProcess == null)
                    {
                        // We are advancing too fast so the block is not ready yet.
                        triggerFlush = true;

                        try
                        {
                            await Task.Delay(2_000, this.calcellation.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }

                        continue;
                    }

                    this.ProcessBlock(blockToProcess, nextHeader);

                    this.chainedHeaderTip = nextHeader;
                    this.addressesIndex.TipHashBytes = this.chainedHeaderTip.HashBlock.ToBytes();
                }

                this.dataStore.Update(this.addressesIndex);
            }
            catch (Exception e)
            {
                this.logger.LogCritical(e.ToString());
            }
        }

        private void AddComponentStats(StringBuilder benchLog)
        {
            benchLog.AppendLine();
            benchLog.AppendLine("======AddressIndexer======");

            benchLog.AppendLine($"Tip: {this.chainedHeaderTip}");
        }

        /// <summary>Processes block that was added or removed from consensus chain.</summary>
        private void ProcessBlock(Block block, ChainedHeader header)
        {
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

                // Record money being spent.
                addrData.Changes.Add(new AddressBalanceChange()
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

                    AddressIndexData addrData = this.GetOrCreateAddressData(txOut.ScriptPubKey);

                    // Record money being sent.
                    addrData.Changes.Add(new AddressBalanceChange()
                    {
                        BalanceChangedHeight = header.Height,
                        Satoshi = amountReceived.Satoshi,
                        Deposited = true
                    });
                }
            }
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
            if (this.storeSettings.TxIndex && this.storeSettings.AddressIndex)
            {
                this.calcellation.Cancel();

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
