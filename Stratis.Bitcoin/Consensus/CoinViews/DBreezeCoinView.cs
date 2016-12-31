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
			Initialize(network.GetGenesis());
		}

		private void Initialize(Block genesis)
		{
			_Session.Do(() =>
			{
				_Session.Transaction.SynchronizeTables("Coins", "BlockHash");
				_Session.Transaction.ValuesLazyLoadingIsOn = false;
			});

			_Session.Do(() =>
			{
				if(GetCurrentHash() == null)
				{
					SetBlockHash(genesis.GetHash());
					//Genesis coin is unspendable so do not add the coins
					_Session.Transaction.Commit();
				}
			});
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

		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			return _Session.Do(() =>
			{
				int insertedEntities = 0;
				using(new StopWatch().Start(o => PerformanceCounter.AddInsertTime(o)))
				{
					var current = GetCurrentHash();
					if(current != oldBlockHash)
						throw new InvalidOperationException("Invalid oldBlockHash");
					SetBlockHash(nextBlockHash);
					var all = unspentOutputs.ToList();
					all.Sort(UnspentOutputsComparer.Instance);
					foreach(var coin in all)
					{
						if(coin.IsPrunable)
							_Session.Transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
						else
							_Session.Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
					}
					insertedEntities += all.Count;
					_Session.Transaction.Commit();
				}
				PerformanceCounter.AddInsertedEntities(insertedEntities);
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
