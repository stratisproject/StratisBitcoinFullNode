using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using DBreeze.DataTypes;

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
        public DBreezeCoinView(Network network, DataFolder dataFolder)
            : this(network, dataFolder.CoinViewPath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="folder">Path to the folder with coinview database files.</param>
        public DBreezeCoinView(Network network, string folder)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            this.session = new DBreezeSingleThreadSession("DBreeze CoinView", folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter();
        }

        /// <summary>
        /// Initializes the database tables used by the coinview.
        /// </summary>
        public Task Initialize()
        {
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

            return Task.WhenAll(new[] { sync, hash });
        }

        /// <inheritdoc />
        public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
        {
            return this.session.Execute(() =>
            {
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

                    return new FetchCoinsResponse(result, blockHash);
                }
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
            this.blockHash = nextBlockHash;
            this.session.Transaction.Insert<byte[], uint256>("BlockHash", blockHashKey, nextBlockHash);
        }

        /// <inheritdoc />
        public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
        {
            return this.session.Execute(() =>
            {
                RewindData rewindData = originalOutputs == null ? null : new RewindData(oldBlockHash);
                int insertedEntities = 0;
                using (new StopWatch().Start(o => this.PerformanceCounter.AddInsertTime(o)))
                {
                    uint256 current = this.GetCurrentHash();
                    if (current != oldBlockHash)
                        throw new InvalidOperationException("Invalid oldBlockHash");

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
                        this.session.Transaction.Insert("Rewind", nextRewindIndex, rewindData);
                    }

                    insertedEntities += all.Count;
                    this.session.Transaction.Commit();
                }

                this.PerformanceCounter.AddInsertedEntities(insertedEntities);
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
                // TODO: Why the result of this.GetRewindIndex() is not reused in the else branch - i.e. why do we call SelectBackward again there to get that very same number?
                if (this.GetRewindIndex() == -1)
                {
                    this.session.Transaction.RemoveAllKeys("Coins", true);
                    this.SetBlockHash(this.network.GenesisHash);
                    this.session.Transaction.Commit();
                    return this.network.GenesisHash;
                }
                else
                {
                    Row<int, RewindData> first = this.session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
                    this.session.Transaction.RemoveKey("Rewind", first.Key);
                    this.SetBlockHash(first.Value.PreviousBlockHash);

                    foreach (var txId in first.Value.TransactionsToRemove)
                    {
                        this.session.Transaction.RemoveKey("Coins", txId.ToBytes(false));
                    }

                    foreach (var coin in first.Value.OutputsToRestore)
                    {
                        this.session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
                    }

                    this.session.Transaction.Commit();
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
                this.PutStakeInternal(stakeEntries);
                this.session.Transaction.Commit();
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
                foreach (StakeItem blockStake in blocklist)
                {
                    Row<byte[], BlockStake> stake = this.session.Transaction.Select<byte[], BlockStake>("Stake", blockStake.BlockId.ToBytes(false));
                    blockStake.BlockStake = stake.Value;
                    blockStake.InStore = true;
                }
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
