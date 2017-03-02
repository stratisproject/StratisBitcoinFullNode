using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using DBreeze;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class DBreezeCoinView : CoinView, IDisposable
	{

		DBreezeSingleThreadSession _Session;
		Network _Network;
		public DBreezeCoinView(Network network, string folder)
		{
			if(folder == null)
				throw new ArgumentNullException("folder");
			if(network == null)
				throw new ArgumentNullException("network");

			_Session = new DBreezeSingleThreadSession("DBreeze CoinView", folder);
			_Network = network;
			Initialize(network.GetGenesis()).GetAwaiter().GetResult(); // hmm...
		}

		private Task Initialize(Block genesis)
		{
			var sync = _Session.Do(() =>
			{
				_Session.Transaction.SynchronizeTables("Coins", "BlockHash", "Rewind");
				_Session.Transaction.ValuesLazyLoadingIsOn = false;
			});

			var hash = _Session.Do(() =>
			{
				if(GetCurrentHash() == null)
				{
					SetBlockHash(genesis.GetHash());
					//Genesis coin is unspendable so do not add the coins
					_Session.Transaction.Commit();
				}
			});

			return Task.WhenAll(new[] { sync, hash });
		}

		static byte[] BlockHashKey = new byte[0];
		static readonly UnspentOutputs[] NoOutputs = new UnspentOutputs[0];
		public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
		{
			return _Session.Do(() =>
			{
				using(StopWatch.Instance.Start(o => PerformanceCounter.AddQueryTime(o)))
				{
					var blockHash = GetCurrentHash();
					UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
					int i = 0;
					PerformanceCounter.AddQueriedEntities(txIds.Length);
					foreach(var input in txIds)
					{
						var coin = _Session.Transaction.Select<byte[], Coins>("Coins", input.ToBytes(false))?.Value;
						result[i++] = coin == null ? null : new UnspentOutputs(input, coin);
					}
					return new FetchCoinsResponse(result, blockHash);
				}
			});
		}


		uint256 _BlockHash;
		private uint256 GetCurrentHash()
		{
			_BlockHash = _BlockHash ?? _Session.Transaction.Select<byte[], uint256>("BlockHash", BlockHashKey)?.Value;
			return _BlockHash;
		}

		private void SetBlockHash(uint256 nextBlockHash)
		{
			_BlockHash = nextBlockHash;
			_Session.Transaction.Insert<byte[], uint256>("BlockHash", BlockHashKey, nextBlockHash);
		}

		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			return _Session.Do(() =>
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
							_Session.Transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
						else
							_Session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
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
						_Session.Transaction.Insert<int, RewindData>("Rewind", nextRewindIndex, rewindData);
					}
					insertedEntities += all.Count;
					_Session.Transaction.Commit();
				}
				PerformanceCounter.AddInsertedEntities(insertedEntities);
			});
		}

		private int GetRewindIndex()
		{
			_Session.Transaction.ValuesLazyLoadingIsOn = true;
			var first = _Session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
			_Session.Transaction.ValuesLazyLoadingIsOn = false;
			return first == null ? -1 : first.Key;
		}

		public override Task<uint256> Rewind()
		{
			return _Session.Do(() =>
			{
				if(GetRewindIndex() == -1)
				{
					_Session.Transaction.RemoveAllKeys("Coins", true);
					SetBlockHash(_Network.GenesisHash);
					_Session.Transaction.Commit();
					return _Network.GenesisHash;
				}
				else
				{
					var first = _Session.Transaction.SelectBackward<int, RewindData>("Rewind").FirstOrDefault();
					_Session.Transaction.RemoveKey("Rewind", first.Key);
					SetBlockHash(first.Value.PreviousBlockHash);
					foreach(var txId in first.Value.TransactionsToRemove)
					{
						_Session.Transaction.RemoveKey("Coins", txId.ToBytes(false));
					}
					foreach(var coin in first.Value.OutputsToRestore)
					{
						_Session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
					}
					_Session.Transaction.Commit();
					return first.Value.PreviousBlockHash;
				}
			});
		}
		private readonly BackendPerformanceCounter _PerformanceCounter = new BackendPerformanceCounter();
		public BackendPerformanceCounter PerformanceCounter
		{
			get
			{
				return _PerformanceCounter;
			}
		}

		public void Dispose()
		{
			_Session.Dispose();
		}
	}
}
