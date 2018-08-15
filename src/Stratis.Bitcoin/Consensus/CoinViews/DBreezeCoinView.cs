using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DBreeze;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.CoinViews
{
    /// <summary>
    /// Persistent implementation of coinview using DBreeze database.
    /// </summary>
    public class DBreezeCoinView : ICoinViewStorage
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

        private const string RewindDataKey = "RewindData";

        private const string CoinsKey = "Coins";

        private const string BlockHashKey = "BlockHash";

        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        public BackendPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="dataFolder">Information about path locations to important folders and files on disk.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public DBreezeCoinView(Network network, DataFolder dataFolder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
            : this(network, dataFolder.CoinViewPath, dateTimeProvider, loggerFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="folder">Path to the folder with coinview database files.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public DBreezeCoinView(Network network, string folder, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            // Create the coinview folder if it does not exist.
            Directory.CreateDirectory(folder);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dbreeze = new DBreezeEngine(folder);
            this.network = network;
            this.PerformanceCounter = new BackendPerformanceCounter(dateTimeProvider);
        }

        /// <inheritdoc />
        public Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            Block genesis = this.network.GetGenesis();

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    transaction.SynchronizeTables(BlockHashKey);

                    if (this.GetTipHash(transaction) == null)
                    {
                        this.SetBlockHash(transaction, genesis.GetHash());

                        // Genesis coin is unspendable so do not add the coins.
                        transaction.Commit();
                    }
                }

                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <inheritdoc />
        public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<uint256> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                uint256 tipHash;

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    tipHash = this.GetTipHash(transaction);
                }

                this.logger.LogTrace("(-):'{0}'", tipHash);
                return tipHash;
            }, cancellationToken);

            return task;
        }

        /// <inheritdoc />
        public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<FetchCoinsResponse> task = Task.Run(() =>
            {
                this.logger.LogTrace("({0}.{1}:{2})", nameof(txIds), nameof(txIds.Length), txIds?.Length);

                FetchCoinsResponse res = null;
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashKey, CoinsKey);
                    transaction.ValuesLazyLoadingIsOn = false;

                    using (new StopwatchDisposable(o => this.PerformanceCounter.AddQueryTime(o)))
                    {
                        uint256 currentTipBlockHash = this.GetTipHash(transaction);
                        var result = new UnspentOutputs[txIds.Length];
                        this.PerformanceCounter.AddQueriedEntities(txIds.Length);

                        int i = 0;
                        foreach (uint256 input in txIds)
                        {
                            Row<byte[], Coins> row = transaction.Select<byte[], Coins>(CoinsKey, input.ToBytes(false));
                            UnspentOutputs outputs = row.Exists ? new UnspentOutputs(input, row.Value) : null;
                            result[i++] = outputs;
                        }

                        res = new FetchCoinsResponse(result, currentTipBlockHash);
                    }
                }

                this.logger.LogTrace("(-):*.{0}='{1}',*.{2}.{3}={4}", nameof(res.BlockHash), res.BlockHash, nameof(res.UnspentOutputs), nameof(res.UnspentOutputs.Length), res.UnspentOutputs.Length);
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
                Row<byte[], uint256> row = transaction.Select<byte[], uint256>(BlockHashKey, blockHashKey);
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
            this.logger.LogTrace("({0}:'{1}')", nameof(nextBlockHash), nextBlockHash);

            this.blockHash = nextBlockHash;
            transaction.Insert<byte[], uint256>(BlockHashKey, blockHashKey, nextBlockHash);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public Task PersistDataAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, List<RewindData> rewindDataCollection, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            Guard.NotNull(unspentOutputs, nameof(unspentOutputs));

            List<UnspentOutputs> allUnspentOutputs = unspentOutputs.ToList();
            this.logger.LogTrace("({0}.Count():{1},{2}.Count():{3},{4}.Count():'{5}',{6}:{7},{8}:{9})", nameof(unspentOutputs), allUnspentOutputs.Count, nameof(originalOutputs), originalOutputs?.Count(), nameof(rewindDataCollection), rewindDataCollection.Count, nameof(oldBlockHash), oldBlockHash, nameof(nextBlockHash), nextBlockHash);

            int insertedEntities = 0;

            Task task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;
                    transaction.SynchronizeTables(BlockHashKey, CoinsKey, RewindDataKey);
                    transaction.Technical_SetTable_OverwriteIsNotAllowed(CoinsKey);

                    using (new StopwatchDisposable(o => this.PerformanceCounter.AddInsertTime(o)))
                    {
                        uint256 current = this.GetTipHash(transaction);
                        if (current != oldBlockHash)
                        {
                            this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                            throw new InvalidOperationException("Invalid oldBlockHash");
                        }

                        this.SetBlockHash(transaction, nextBlockHash);

                        allUnspentOutputs.Sort(UnspentOutputsComparer.Instance);
                        foreach (UnspentOutputs coin in allUnspentOutputs)
                        {
                            this.logger.LogTrace("Outputs of transaction ID '{transactionId}' are {prunableState} and will be {action} to the database.", coin.TransactionId, coin.IsPrunable ? "PRUNABLE" : "NOT PRUNABLE", coin.IsPrunable ? "removed" : "inserted");
                            if (coin.IsPrunable) transaction.RemoveKey(CoinsKey, coin.TransactionId.ToBytes(false));
                            else transaction.Insert(CoinsKey, coin.TransactionId.ToBytes(false), coin.ToCoins());
                        }

                        foreach (RewindData rewindData in rewindDataCollection)
                        {
                            int nextRewindIndex = 0;
                            if (this.TryGetRewindIndex(transaction, out int currentRewindIndex))
                            {
                                nextRewindIndex = currentRewindIndex + 1;
                            }

                            this.logger.LogTrace("Rewind state #{0} created.", nextRewindIndex);
                            transaction.Insert(RewindDataKey, nextRewindIndex, rewindData);
                        }

                        insertedEntities += allUnspentOutputs.Count;
                        transaction.Commit();
                    }
                }

                this.PerformanceCounter.AddInsertedEntities(insertedEntities);
                this.logger.LogTrace("(-)");
            });

            this.logger.LogTrace("(-)");
            return task;
        }

        /// <summary>
        /// Attempts to obtain order number of the last saved rewind state in the database.
        /// </summary>
        /// <param name="transaction">Open DBreeze transaction.</param>
        /// <param name="rewindDataCollectionIndex">Order number of the last saved rewind state.</param>
        /// <returns><c>True</c>, if rewind data collection index was located. Otherwise, <c>False</c> is returned.</returns>
        private bool TryGetRewindIndex(DBreeze.Transactions.Transaction transaction, out int rewindDataCollectionIndex)
        {
            bool prevLazySettings = transaction.ValuesLazyLoadingIsOn;

            transaction.ValuesLazyLoadingIsOn = true;
            Row<int, RewindData> firstRow = transaction.SelectBackward<int, RewindData>(RewindDataKey).FirstOrDefault();
            transaction.ValuesLazyLoadingIsOn = prevLazySettings;

            if (firstRow == null)
            {
                rewindDataCollectionIndex = -1;
                return false;
            }

            rewindDataCollectionIndex = firstRow.Key;
            return true;
        }

        /// <inheritdoc />
        public Task<uint256> Rewind()
        {
            Task<uint256> task = Task.Run(() =>
            {
                this.logger.LogTrace("()");

                uint256 res = null;
                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables(BlockHashKey, CoinsKey, RewindDataKey);
                    if (!this.TryGetRewindIndex(transaction, out int unused))
                    {
                        transaction.RemoveAllKeys(CoinsKey, true);
                        this.SetBlockHash(transaction, this.network.GenesisHash);

                        res = this.network.GenesisHash;
                    }
                    else
                    {
                        transaction.ValuesLazyLoadingIsOn = false;

                        Row<int, RewindData> firstRow = transaction.SelectBackward<int, RewindData>(RewindDataKey).FirstOrDefault();
                        transaction.RemoveKey(RewindDataKey, firstRow.Key);
                        this.SetBlockHash(transaction, firstRow.Value.PreviousBlockHash);

                        foreach (uint256 txId in firstRow.Value.TransactionsToRemove)
                        {
                            this.logger.LogTrace("Outputs of transaction ID '{0}' will be removed.", txId);
                            transaction.RemoveKey(CoinsKey, txId.ToBytes(false));
                        }

                        foreach (UnspentOutputs coin in firstRow.Value.OutputsToRestore)
                        {
                            this.logger.LogTrace("Outputs of transaction ID '{0}' will be restored.", coin.TransactionId);
                            transaction.Insert(CoinsKey, coin.TransactionId.ToBytes(false), coin.ToCoins());
                        }

                        res = firstRow.Value.PreviousBlockHash;
                    }

                    transaction.Commit();
                }

                this.logger.LogTrace("(-):'{0}'", res);
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
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeEntries), stakeEntries.Count());

                using (DBreeze.Transactions.Transaction transaction = this.dbreeze.GetTransaction())
                {
                    transaction.SynchronizeTables("Stake");
                    this.PutStakeInternal(transaction, stakeEntries);
                    transaction.Commit();
                }

                this.logger.LogTrace("(-)");
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
                this.logger.LogTrace("({0}.Count():{1})", nameof(blocklist), blocklist.Count());

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

                    this.logger.LogTrace("(-)");
                }
            });

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.dbreeze.Dispose();
        }
    }
}
