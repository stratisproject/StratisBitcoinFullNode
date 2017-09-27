using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Persistent implementation of coinview using DBreeze database.
    /// </summary>
    public class DBreezeCoinView : CoinView, IDisposable
    {
        /// <summary>Database key under which the block hash of the coin view's current tip is stored.</summary>
        private static readonly byte[] blockHashKey = new byte[0];

        /// TODO: Can we removed this? It is not used anywhere.
        private static readonly UnspentOutputs[] noOutputs = new UnspentOutputs[0];

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Session providing access to DBreeze database.</summary>
        private readonly DBreezeSingleThreadSession session;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Hash of the block which is currently the tip of the coinview.</summary>
        private uint256 blockHash;

        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        private readonly BackendPerformanceCounter performanceCounter;
        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        public BackendPerformanceCounter PerformanceCounter { get { return this.performanceCounter; } }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="dataFolder">Information about path locations to important folders and files on disk.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public DBreezeCoinView(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory)
            : this(network, dataFolder.CoinViewPath, loggerFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="folder">Path to the folder with coinview database files.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public DBreezeCoinView(Network network, string folder, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.session = new DBreezeSingleThreadSession("DBreeze CoinView", folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter();
        }

        /// <summary>
        /// Initializes the database tables used by the coinview.
        /// </summary>
        public Task Initialize()
        {
            this.logger.LogTrace("()");

            Block genesis = this.network.GetGenesis();

            Task sync = this.session.Execute(() =>
            {
                this.session.Transaction.SynchronizeTables("Coins", "BlockHash", "Rewind", "Stake");
                this.session.Transaction.ValuesLazyLoadingIsOn = false;
            });

            Task hash = this.session.Execute(() =>
            {
                if (this.GetCurrentHash() == null)
                {
                    this.SetBlockHash(genesis.GetHash());
                    // Genesis coin is unspendable so do not add the coins.
                    this.session.Transaction.Commit();
                }
            });

            this.logger.LogTrace("(-)");
            return Task.WhenAll(new[] { sync, hash });
        }

        /// <inheritdoc />
        public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
        {
            return this.session.Execute(() =>
            {
                this.logger.LogTrace("({0}.{1}:{2})", nameof(txIds), nameof(txIds.Length), txIds?.Length);

                FetchCoinsResponse res = null;
                using (StopWatch.Instance.Start(o => this.PerformanceCounter.AddQueryTime(o)))
                {
                    uint256 blockHash = this.GetCurrentHash();
                    UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
                    this.PerformanceCounter.AddQueriedEntities(txIds.Length);

                    int i = 0;
                    foreach (uint256 input in txIds)
                    {
                        Coins coin = this.session.Transaction.Select<byte[], Coins>("Coins", input.ToBytes(false))?.Value;
                        result[i++] = coin == null ? null : new UnspentOutputs(input, coin);
                    }

                    res = new FetchCoinsResponse(result, blockHash);
                }

                this.logger.LogTrace("(-):*.{0}='{1}',*.{2}.{3}={4}", nameof(res.BlockHash), res.BlockHash, nameof(res.UnspentOutputs), nameof(res.UnspentOutputs.Length), res.UnspentOutputs.Length);
                return res;
            });
        }

        /// <summary>
        /// Obtains a block header hash of the coinview's current tip.
        /// </summary>
        /// <returns>Block header hash of the coinview's current tip.</returns>
        private uint256 GetCurrentHash()
        {
            this.blockHash = this.blockHash ?? this.session.Transaction.Select<byte[], uint256>("BlockHash", blockHashKey)?.Value;
            return this.blockHash;
        }

        /// <summary>
        /// Set's the tip of the coinview to a new block hash.
        /// </summary>
        /// <param name="nextBlockHash">Hash of the block to become the new tip.</param>
        private void SetBlockHash(uint256 nextBlockHash)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(nextBlockHash), nextBlockHash);

            this.blockHash = nextBlockHash;
            this.session.Transaction.Insert<byte[], uint256>("BlockHash", blockHashKey, nextBlockHash);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            return this.session.Execute(() =>
            {
                this.logger.LogTrace("({0}.Count():{1},{2}.Count():{3},{4}:'{5}',{6}:'{7}')", nameof(unspentOutputs), unspentOutputs?.Count(), nameof(originalOutputs), originalOutputs?.Count(), nameof(oldBlockHash), oldBlockHash, nameof(nextBlockHash), nextBlockHash);

                RewindData rewindData = originalOutputs == null ? null : new RewindData(oldBlockHash);
                int insertedEntities = 0;
                using (new StopWatch().Start(o => this.PerformanceCounter.AddInsertTime(o)))
                {
                    uint256 current = this.GetCurrentHash();
                    if (current != oldBlockHash)
                    {
                        this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                        throw new InvalidOperationException("Invalid oldBlockHash");
                    }

                    this.SetBlockHash(nextBlockHash);
                    List<UnspentOutputs> all = unspentOutputs.ToList();
                    Dictionary<uint256, TxOut[]> unspentToOriginal = new Dictionary<uint256, TxOut[]>(all.Count);
                    if (originalOutputs != null)
                    {
                        IEnumerator<TxOut[]> originalEnumerator = originalOutputs.GetEnumerator();
                        foreach (UnspentOutputs output in all)
                        {
                            originalEnumerator.MoveNext();
                            unspentToOriginal.Add(output.TransactionId, originalEnumerator.Current);
                        }
                    }

                    all.Sort(UnspentOutputsComparer.Instance);
                    foreach (UnspentOutputs coin in all)
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' are {1} and will be {2} to the database.", coin.TransactionId, coin.IsPrunable ? "PRUNABLE" : "NOT PRUNABLE", coin.IsPrunable ? "removed" : "inserted");
                        if (coin.IsPrunable) this.session.Transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
                        else this.session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());

                        if (originalOutputs != null)
                        {
                            TxOut[] original = null;
                            unspentToOriginal.TryGetValue(coin.TransactionId, out original);
                            if (original == null)
                            {
                                // This one haven't existed before, if we rewind, delete it.
                                rewindData.TransactionsToRemove.Add(coin.TransactionId);
                            }
                            else
                            {
                                // We'll need to restore the original outputs.
                                UnspentOutputs clone = coin.Clone();
                                // TODO: Can we remove this line? before is not used anywhere and clone.UnspentCount does not seem to have side effects?
                                int before = clone.UnspentCount;
                                clone._Outputs = original.ToArray();
                                rewindData.OutputsToRestore.Add(clone);
                            }
                        }
                    }

                    if (rewindData != null)
                    {
                        int nextRewindIndex = this.GetRewindIndex() + 1;
                        this.logger.LogTrace("Rewind state #{0} created.", nextRewindIndex);
                        this.session.Transaction.Insert("Rewind", nextRewindIndex, rewindData);
                    }

                    insertedEntities += all.Count;
                    this.session.Transaction.Commit();
                }

                this.PerformanceCounter.AddInsertedEntities(insertedEntities);
                this.logger.LogTrace("(-)");
            });
        }

        /// <summary>
        /// Obtains order number of the last saved rewind state in the database.
        /// </summary>
        /// <returns>Order number of the last saved rewind state, or <c>-1</c> if no rewind state is found in the database.</returns>
        /// <remarks>TODO: Using <c>-1</c> is hacky here, and <see cref="SaveChangesAsync"/> exploits that in a way that if no such rewind data exist 
        /// the order number of the first rewind data is -1 + 1 = 0.</remarks>
        private int GetRewindIndex()
        {
            this.session.Transaction.ValuesLazyLoadingIsOn = true;
            Row<int, RewindData> first = this.session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
            this.session.Transaction.ValuesLazyLoadingIsOn = false;
            return first == null ? -1 : first.Key;
        }

        /// <inheritdoc />
        public override Task<uint256> Rewind()
        {
            return this.session.Execute(() =>
            {
                this.logger.LogTrace("()");

                if (this.GetRewindIndex() == -1)
                {
                    this.session.Transaction.RemoveAllKeys("Coins", true);
                    this.SetBlockHash(this.network.GenesisHash);
                    this.session.Transaction.Commit();

                    this.logger.LogTrace("(-)[REWOUND_TO_GENESIS]:'{0}'", this.network.GenesisHash);
                    return this.network.GenesisHash;
                }
                else
                {
                    Row<int, RewindData> first = this.session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
                    this.session.Transaction.RemoveKey("Rewind", first.Key);
                    this.SetBlockHash(first.Value.PreviousBlockHash);

                    foreach (uint256 txId in first.Value.TransactionsToRemove)
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' will be removed.", txId);
                        this.session.Transaction.RemoveKey("Coins", txId.ToBytes(false));
                    }

                    foreach (UnspentOutputs coin in first.Value.OutputsToRestore)
                    {
                        this.logger.LogTrace("Outputs of transaction ID '{0}' will be restored.", coin.TransactionId);
                        this.session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
                    }

                    this.session.Transaction.Commit();

                    this.logger.LogTrace("(-):'{0}'", first.Value.PreviousBlockHash);
                    return first.Value.PreviousBlockHash;
                }
            });
        }

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        public Task PutStake(IEnumerable<StakeItem> stakeEntries)
        {
            return this.session.Execute(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(stakeEntries), stakeEntries.Count());

                this.PutStakeInternal(stakeEntries);
                this.session.Transaction.Commit();

                this.logger.LogTrace("(-)");
            });
        }

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        private void PutStakeInternal(IEnumerable<StakeItem> stakeEntries)
        {
            foreach (StakeItem stakeEntry in stakeEntries)
            {
                if (!stakeEntry.InStore)
                {
                    this.session.Transaction.Insert<byte[], BlockStake>("Stake", stakeEntry.BlockId.ToBytes(false), stakeEntry.BlockStake);
                    stakeEntry.InStore = true;
                }
            }
        }

        /// <summary>
        /// Retrieves POS blocks information from the database.
        /// </summary>
        /// <param name="blocklist">List of partially initialized POS block information that is to be fully initialized with the values from the database.</param>
        public Task GetStake(IEnumerable<StakeItem> blocklist)
        {
            return this.session.Execute(() =>
            {
                this.logger.LogTrace("({0}.Count():{1})", nameof(blocklist), blocklist.Count());

                foreach (StakeItem blockStake in blocklist)
                {
                    this.logger.LogTrace("Loading POS block hash '{0}' from the database.", blockStake.BlockId);
                    Row<byte[], BlockStake> stake = this.session.Transaction.Select<byte[], BlockStake>("Stake", blockStake.BlockId.ToBytes(false));
                    blockStake.BlockStake = stake.Value;
                    blockStake.InStore = true;
                }

                this.logger.LogTrace("(-)");
            });
        }

        /// TODO: Do we need this method? 
        public Task DeleteStake(uint256 blockid, BlockStake blockStake)
        {
            // TODO: Implement delete stake on rewind.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.session.Dispose();
        }
    }
}
