using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using DBreeze;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Consensus
{
	public class DBreezeCoinView : CoinView, IDisposable
	{
		private readonly DBreezeSingleThreadSession session;
		private readonly Network network;
		private uint256 blockHash;
		private readonly BackendPerformanceCounter performanceCounter;

		public DBreezeCoinView(Network network, DataFolder dataFolder) 
			: this(network, dataFolder.CoinViewPath)
		{
		}

		public DBreezeCoinView(Network network, string folder)
		{
			Guard.NotNull(network, nameof(network));
			Guard.NotEmpty(folder, nameof(folder));
			
			session = new DBreezeSingleThreadSession("DBreeze CoinView", folder);
			this.network = network;
			this.performanceCounter = new BackendPerformanceCounter();
		}

		static readonly byte[] BlockHashKey = new byte[0];
		static readonly UnspentOutputs[] NoOutputs = new UnspentOutputs[0];

		public BackendPerformanceCounter PerformanceCounter
		{
			get
			{
				return performanceCounter;
			}
		}

		public Task Initialize()
		{
			var genesis = this.network.GetGenesis();

			var sync = session.Do(() =>
			{
				session.Transaction.SynchronizeTables("Coins", "BlockHash", "Rewind", "Stake");
				session.Transaction.ValuesLazyLoadingIsOn = false;
			});

			var hash = session.Do(() =>
			{
				if(GetCurrentHash() == null)
				{
					SetBlockHash(genesis.GetHash());
					//Genesis coin is unspendable so do not add the coins
					session.Transaction.Commit();
				}
			});

			return Task.WhenAll(new[] { sync, hash });
		}

		public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
		{
			return session.Do(() =>
			{
				using(StopWatch.Instance.Start(o => PerformanceCounter.AddQueryTime(o)))
				{
					var blockHash = GetCurrentHash();
					UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
					int i = 0;
					PerformanceCounter.AddQueriedEntities(txIds.Length);
					foreach(var input in txIds)
					{
						var coin = session.Transaction.Select<byte[], Coins>("Coins", input.ToBytes(false))?.Value;
						result[i++] = coin == null ? null : new UnspentOutputs(input, coin);
					}
					return new FetchCoinsResponse(result, blockHash);
				}
			});
		}

		private uint256 GetCurrentHash()
		{
			blockHash = blockHash ?? session.Transaction.Select<byte[], uint256>("BlockHash", BlockHashKey)?.Value;
			return blockHash;
		}

		private void SetBlockHash(uint256 nextBlockHash)
		{
			blockHash = nextBlockHash;
			session.Transaction.Insert<byte[], uint256>("BlockHash", BlockHashKey, nextBlockHash);
		}

		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			return session.Do(() =>
			{
				RewindData rewindData = originalOutputs == null ? null : new RewindData(oldBlockHash);
				int insertedEntities = 0;
				using(new StopWatch().Start(o => PerformanceCounter.AddInsertTime(o)))
				{
					var current = GetCurrentHash();
					if(current != oldBlockHash)
						throw new InvalidOperationException("Invalid oldBlockHash");
					SetBlockHash(nextBlockHash);
					var all = unspentOutputs.ToList();
					Dictionary<uint256, TxOut[]> unspentToOriginal = new Dictionary<uint256, TxOut[]>(all.Count);
					if(originalOutputs != null)
					{
						var originalEnumerator = originalOutputs.GetEnumerator();
						foreach(var u in all)
						{
							originalEnumerator.MoveNext();
							unspentToOriginal.Add(u.TransactionId, originalEnumerator.Current);
						}
					}
					all.Sort(UnspentOutputsComparer.Instance);
					foreach(var coin in all)
					{
						if(coin.IsPrunable)
							session.Transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
						else
							session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
						if(originalOutputs != null)
						{
							TxOut[] original = null;
							unspentToOriginal.TryGetValue(coin.TransactionId, out original);
							if(original == null)
							{
								//This one did not existed before, if we rewind, delete it
								rewindData.TransactionsToRemove.Add(coin.TransactionId);
							}
							else
							{
								//We'll need to restore the original outputs
								var clone = coin.Clone();
								var before = clone.UnspentCount;
								clone._Outputs = original.ToArray();
								rewindData.OutputsToRestore.Add(clone);
							}
						}
					}
					if(rewindData != null)
					{
						int nextRewindIndex = GetRewindIndex() + 1;
						session.Transaction.Insert<int, RewindData>("Rewind", nextRewindIndex, rewindData);
					}
					insertedEntities += all.Count;
					session.Transaction.Commit();
				}
				PerformanceCounter.AddInsertedEntities(insertedEntities);
			});
		}

		private int GetRewindIndex()
		{
			session.Transaction.ValuesLazyLoadingIsOn = true;
			var first = session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
			session.Transaction.ValuesLazyLoadingIsOn = false;
			return first == null ? -1 : first.Key;
		}

		public override Task<uint256> Rewind()
		{
			return session.Do(() =>
			{
				if(GetRewindIndex() == -1)
				{
					session.Transaction.RemoveAllKeys("Coins", true);
					SetBlockHash(network.GenesisHash);
					session.Transaction.Commit();
					return network.GenesisHash;
				}
				else
				{
					var first = session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
					session.Transaction.RemoveKey("Rewind", first.Key);
					SetBlockHash(first.Value.PreviousBlockHash);
					foreach(var txId in first.Value.TransactionsToRemove)
					{
						session.Transaction.RemoveKey("Coins", txId.ToBytes(false));
					}
					foreach(var coin in first.Value.OutputsToRestore)
					{
						session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
					}
					session.Transaction.Commit();
					return first.Value.PreviousBlockHash;
				}
			});
		}

		public Task PutStake(IEnumerable<StakeItem> stakeEntries)
		{
			return session.Do(() =>
			{
				this.PutStakeInternal(stakeEntries);
				session.Transaction.Commit();
			});
		}

		private void PutStakeInternal(IEnumerable<StakeItem> stakeEntries)
		{
			foreach (var stakeEntry in stakeEntries)
			{
				if (!stakeEntry.InStore)
				{
					session.Transaction.Insert<byte[], BlockStake>("Stake", stakeEntry.BlockId.ToBytes(false), stakeEntry.BlockStake);
					stakeEntry.InStore = true;
				}
			}
		}

		public Task GetStake(IEnumerable<StakeItem> blocklist)
		{
			return session.Do(() =>
			{
				foreach (var blockStake in blocklist)
				{
					var stake = session.Transaction.Select<byte[], BlockStake>("Stake", blockStake.BlockId.ToBytes(false));
					blockStake.BlockStake = stake.Value;
					blockStake.InStore = true;
				}
			});
		}

		public Task DeleteStake(uint256 blockid, BlockStake blockStake)
		{
			// TODO: implement delete stake on rewind
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			session.Dispose();
		}
	}
}
