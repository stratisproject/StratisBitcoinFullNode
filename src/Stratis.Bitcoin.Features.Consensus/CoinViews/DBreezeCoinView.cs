﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Persistent implementation of coinview using DBreeze database.
    /// </summary>
    public class DBreezeCoinView : ICoinView, IDisposable
    {
        /// <summary>Database key under which the block hash of the coin view's current tip is stored.</summary>
        private static readonly byte[] blockHashKey = new byte[0];

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly DBreezeEngine dbreeze;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Hash of the block which is currently the tip of the coinview.</summary>
        private uint256 blockHash;

        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        private readonly BackendPerformanceCounter performanceCounter;

        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="dataFolder">Information about path locations to important folders and files on disk.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public DBreezeCoinView(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
            : this(network, dataFolder.CoinViewPath, dateTimeProvider, loggerFactory, nodeStats)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="folder">Path to the folder with coinview database files.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public DBreezeCoinView(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, INodeStats nodeStats)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            // Create the coinview folder if it does not exist.
            Directory.CreateDirectory(folder);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, 400);
        }

        /// <summary>
        /// Initializes the database tables used by the coinview.
        /// </summary>
        public Task InitializeAsync()
        {
            Block genesis = this.network.GetGenesis();

            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    transaction.SynchronizeTables("BlockHash");

                    if (this.GetTipHash(transaction) == null)
                    {
                        this.SetBlockHash(transaction, genesis.GetHash());

                        // Genesis coin is unspendable so do not add the coins.
                        transaction.Commit();
                    }
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<uint256> task = Task.Run(() =>
            {
                uint256 tipHash;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    tipHash = this.GetTipHash(transaction);
                }

                return tipHash;
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<FetchCoinsResponse> task = Task.Run(() =>
            {
                FetchCoinsResponse res = null;
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("BlockHash", "Coins");
                    transaction.ValuesLazyLoadingIsOn = false;

                    using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
                    {
                        uint256 blockHash = this.GetTipHash(transaction);
                        var result = new UnspentOutputs[txIds.Length];
                        this.performanceCounter.AddQueriedEntities(txIds.Length);

                        int i = 0;
                        foreach (uint256 input in txIds)
                        {
                            Row<byte[], Coins> row = transaction.Select<byte[], Coins>("Coins", input.ToBytes(false));
                            UnspentOutputs outputs = row.Exists ? new UnspentOutputs(input, row.Value) : null;

                            this.logger.LogTrace("Outputs for '{0}' were {1}.", input, outputs == null ? "NOT loaded" : "loaded");

                            result[i++] = outputs;
                        }

                        res = new FetchCoinsResponse(result, blockHash);
                    }
                }

                return res;
            }, cancellationToken);

            return task;
        }

        /// <summary>
        /// Obtains a block header hash of the coinview's current tip.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <returns>Block header hash of the coinview's current tip.</returns>
        private uint256 GetTipHash(DBreeze.Transactions.Transaction transaction)
        {
            if (this.blockHash == null)
            {
                Row<byte[], uint256> row = transaction.Select<byte[], uint256>("BlockHash", blockHashKey);
                if (row.Exists)
                    this.blockHash = row.Value;
            }

            return this.blockHash;
        }

        /// <summary>
        /// Set's the tip of the coinview to a new block hash.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="nextBlockHash">Hash of the block to become the new tip.</param>
        private void SetBlockHash(DBreeze.Transactions.Transaction transaction, uint256 nextBlockHash)
        {
            this.blockHash = nextBlockHash;
            transaction.Insert<byte[], uint256>("BlockHash", blockHashKey, nextBlockHash);
        }

        /// <inheritdoc />
        public Task SaveChangesAsync(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, List<RewindData> rewindDataList = null)
        {
            Task task = Task.Run(() =>
            {
                int insertedEntities = 0;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    transaction.SynchronizeTables("BlockHash", "Coins", "Rewind");

                    // Speed can degrade when keys are in random order and, especially, if these keys have high entropy.
                    // This settings helps with speed, see DBreeze documentations about details.
                    // We should double check if this settings help in our scenario, or sorting keys and operations is enough.
                    // Refers to issue #2483. https://github.com/stratisproject/StratisBitcoinFullNode/issues/2483
                    transaction.Technical_SetTable_OverwriteIsNotAllowed("Coins");

                    using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
                    {
                        uint256 current = this.GetTipHash(transaction);
                        if (current != oldBlockHash)
                        {
                            this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                            throw new InvalidOperationException("Invalid oldBlockHash");
                        }

                        this.SetBlockHash(transaction, nextBlockHash);

                        // Here we'll add items to be inserted in a second pass.
                        List<UnspentOutputs> toInsert = new List<UnspentOutputs>();

                        foreach (var coin in unspentOutputs.OrderBy(utxo => utxo.TransactionId, new UInt256Comparer()))
                        {
                            if (coin.IsPrunable)
                            {
                                this.logger.LogTrace("Outputs of transaction ID '{0}' are prunable and will be removed from the database.", coin.TransactionId);
                                transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
                            }
                            else
                            {
                                // Add the item to another list that will be used in the second pass.
                                // This is for performance reasons: DBreeze is optimized to run the same kind of operations, sorted.
                                toInsert.Add(coin);
                            }
                        }

                        for (int i = 0; i < toInsert.Count; i++)
                        {
                            var coin = toInsert[i];
                            this.logger.LogTrace("Outputs of transaction ID '{0}' are NOT PRUNABLE and will be inserted into the database. {1}/{2}.", coin.TransactionId, i, toInsert.Count);

                            transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
                        }

                        if (rewindDataList != null)
                        {
                            int nextRewindIndex = this.GetRewindIndex(transaction) + 1;
                            foreach (RewindData rewindData in rewindDataList)
                            {
                                this.logger.LogTrace("Rewind state #{0} created.", nextRewindIndex);

                                transaction.Insert("Rewind", nextRewindIndex, rewindData);
                                nextRewindIndex++;
                            }
                        }

                        insertedEntities += unspentOutputs.Count;
                        transaction.Commit();
                    }
                }

                this.performanceCounter.AddInsertedEntities(insertedEntities);
            });

            return task;
        }

        /// <summary>
        /// Obtains order number of the last saved rewind state in the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <returns>Order number of the last saved rewind state, or <c>-1</c> if no rewind state is found in the database.</returns>
        /// <remarks>TODO: Using <c>-1</c> is hacky here, and <see cref="SaveChangesAsync"/> exploits that in a way that if no such rewind data exist
        /// the order number of the first rewind data is -1 + 1 = 0.</remarks>
        private int GetRewindIndex(DBreeze.Transactions.Transaction transaction)
        {
            bool prevLazySettings = transaction.ValuesLazyLoadingIsOn;

            transaction.ValuesLazyLoadingIsOn = true;
            Row<int, RewindData> firstRow = transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
            transaction.ValuesLazyLoadingIsOn = prevLazySettings;

            return firstRow != null ? firstRow.Key : -1;
        }

        /// <inheritdoc />
        public Task<uint256> RewindAsync()
        {
            Task<uint256> task = Task.Run(() =>
            {
                uint256 res = null;
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("BlockHash", "Coins", "Rewind");
                    if (this.GetRewindIndex(transaction) == -1)
                    {
                        transaction.RemoveAllKeys("Coins", true);
                        this.SetBlockHash(transaction, this.network.GenesisHash);

                        res = this.network.GenesisHash;
                    }
                    else
                    {
                        transaction.ValuesLazyLoadingIsOn = false;

                        Row<int, RewindData> firstRow = transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
                        transaction.RemoveKey("Rewind", firstRow.Key);
                        this.SetBlockHash(transaction, firstRow.Value.PreviousBlockHash);

                        foreach (uint256 txId in firstRow.Value.TransactionsToRemove)
                        {
                            this.logger.LogTrace("Outputs of transaction ID '{0}' will be removed.", txId);
                            transaction.RemoveKey("Coins", txId.ToBytes(false));
                        }

                        foreach (UnspentOutputs coin in firstRow.Value.OutputsToRestore)
                        {
                            this.logger.LogTrace("Outputs of transaction ID '{0}' will be restored.", coin.TransactionId);
                            transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
                        }

                        res = firstRow.Value.PreviousBlockHash;
                    }

                    transaction.Commit();
                }

                return res;
            });

            return task;
        }

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        public Task PutStakeAsync(IEnumerable<StakeItem> stakeEntries)
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("Stake");
                    this.PutStakeInternal(transaction, stakeEntries);
                    transaction.Commit();
                }
            });

            return task;
        }

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        private void PutStakeInternal(DBreeze.Transactions.Transaction transaction, IEnumerable<StakeItem> stakeEntries)
        {
            foreach (StakeItem stakeEntry in stakeEntries)
            {
                if (!stakeEntry.InStore)
                {
                    transaction.Insert<byte[], BlockStake>("Stake", stakeEntry.BlockId.ToBytes(false), stakeEntry.BlockStake);
                    stakeEntry.InStore = true;
                }
            }
        }

        /// <summary>
        /// Retrieves POS blocks information from the database.
        /// </summary>
        /// <param name="blocklist">List of partially initialized POS block information that is to be fully initialized with the values from the database.</param>
        public Task GetStakeAsync(IEnumerable<StakeItem> blocklist)
        {
            Task task = Task.Run(() =>
            {
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("Stake");
                    transaction.ValuesLazyLoadingIsOn = false;

                    foreach (StakeItem blockStake in blocklist)
                    {
                        this.logger.LogTrace("Loading POS block hash '{0}' from the database.", blockStake.BlockId);
                        Row<byte[], BlockStake> stakeRow = transaction.Select<byte[], BlockStake>("Stake", blockStake.BlockId.ToBytes(false));

                        if (stakeRow.Exists)
                        {
                            blockStake.BlockStake = stakeRow.Value;
                            blockStake.InStore = true;
                        }
                    }
                }
            });

            return task;
        }

        private void AddBenchStats(StringBuilder log)
        {
            log.AppendLine("======DBreezeCoinView Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                log.AppendLine(snapShot.ToString());
            else
                log.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}
