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
    public class DBreezeCoinView : CoinView, IDisposable
    {
        private static readonly byte[] blockHashKey = new byte[0];
        private static readonly UnspentOutputs[] noOutputs = new UnspentOutputs[0];

        private readonly DBreezeSingleThreadSession session;
        private readonly Network network;
        private uint256 blockHash;
        private readonly BackendPerformanceCounter performanceCounter;

        public BackendPerformanceCounter PerformanceCounter { get { return this.performanceCounter; } }

        public DBreezeCoinView(Network network, DataFolder dataFolder)
            : this(network, dataFolder.CoinViewPath)
        {
        }

        public DBreezeCoinView(Network network, string folder)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotEmpty(folder, nameof(folder));

            this.session = new DBreezeSingleThreadSession("DBreeze CoinView", folder);
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter();
        }

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

        private uint256 GetCurrentHash()
        {
            this.blockHash = this.blockHash ?? this.session.Transaction.Select<byte[], uint256>("BlockHash", blockHashKey)?.Value;
            return this.blockHash;
        }

        private void SetBlockHash(uint256 nextBlockHash)
        {
            this.blockHash = nextBlockHash;
            this.session.Transaction.Insert<byte[], uint256>("BlockHash", blockHashKey, nextBlockHash);
        }

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
                                // This one did not existed before, if we rewind, delete it.
                                rewindData.TransactionsToRemove.Add(coin.TransactionId);
                            }
                            else
                            {
                                // We'll need to restore the original outputs.
                                UnspentOutputs clone = coin.Clone();
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

        private int GetRewindIndex()
        {
            this.session.Transaction.ValuesLazyLoadingIsOn = true;
            Row<int, RewindData> first = this.session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
            this.session.Transaction.ValuesLazyLoadingIsOn = false;
            return first == null ? -1 : first.Key;
        }

        public override Task<uint256> Rewind()
        {
            return this.session.Execute(() =>
            {
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

        public Task PutStake(IEnumerable<StakeItem> stakeEntries)
        {
            return this.session.Execute(() =>
            {
                this.PutStakeInternal(stakeEntries);
                this.session.Transaction.Commit();
            });
        }

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

        public Task DeleteStake(uint256 blockid, BlockStake blockStake)
        {
            // TODO: Implement delete stake on rewind.
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            this.session.Dispose();
        }
    }
}
