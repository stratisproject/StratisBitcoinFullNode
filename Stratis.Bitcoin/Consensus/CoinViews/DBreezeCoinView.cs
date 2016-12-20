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
		DBreezeEngine _Engine;
		string _Folder;
		Network _Network;
		public DBreezeCoinView(Network network, string folder)
		{
			if(folder == null)
				throw new ArgumentNullException("folder");
			if(network == null)
				throw new ArgumentNullException("network");
			_Folder = folder;
			_Network = network;
			_SingleThread = new CustomThreadPoolTaskScheduler(1, 100, "DBreeze");
			Initialize();
		}

		public void Initialize(Block genesis)
		{
			new Task(() =>
			{
				SetBlockHash(genesis.GetHash());
				//Genesis coin is unspendable so do not add the coins
				_Transaction.Commit();
			}).Start(_SingleThread);
		}

		void Initialize()
		{
			DBreeze.Utils.CustomSerializator.ByteArraySerializator = NBitcoinSerialize;
			DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = NBitcoinDeserialize;

			new Task(() =>
			{
				_Engine = new DBreezeEngine(_Folder);
				_Transaction = _Engine.GetTransaction();
				_Transaction.ValuesLazyLoadingIsOn = false;
			}).Start(_SingleThread);
		}

		DBreeze.Transactions.Transaction _Transaction;
		CustomThreadPoolTaskScheduler _SingleThread;

		byte[] NBitcoinSerialize(object obj)
		{
			IBitcoinSerializable serializable = obj as IBitcoinSerializable;
			if(serializable != null)
				return serializable.ToBytes();
			uint256 u = obj as uint256;
			if(u != null)
				return u.ToBytes();
			throw new NotSupportedException();
		}
		object NBitcoinDeserialize(byte[] bytes, Type type)
		{
			if(type == typeof(Coins))
			{
				Coins coin = new Coins();
				coin.ReadWrite(bytes);
				return coin;
			}
			if(type == typeof(BlockHeader))
			{
				BlockHeader header = new BlockHeader();
				header.ReadWrite(bytes);
				return header;
			}
			if(type == typeof(uint256))
			{
				return new uint256(bytes);
			}
			throw new NotSupportedException();
		}

		static byte[] BlockHashKey = new byte[0];
		static readonly UnspentOutputs[] NoOutputs = new UnspentOutputs[0];
		public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
		{
			var task = new Task<FetchCoinsResponse>(() =>
			{
				using(StopWatch.Instance.Start(o => PerformanceCounter.AddQueryTime(o)))
				{
					var blockHash = GetCurrentHash();
					UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
					int i = 0;
					PerformanceCounter.AddQueriedEntities(txIds.Length);
					foreach(var input in txIds)
					{						
						var coin = _Transaction.Select<byte[], Coins>("Coins", input.ToBytes(false))?.Value;
						result[i++] = coin == null ? null : new UnspentOutputs(input, coin);
					}
					return new FetchCoinsResponse(result, blockHash);
				}
			});
			task.Start(_SingleThread);
			return task;
		}


		uint256 _BlockHash;
		private uint256 GetCurrentHash()
		{
			_BlockHash = _BlockHash ?? _Transaction.Select<byte[], uint256>("BlockHash", BlockHashKey)?.Value;
			return _BlockHash;
		}

		private void SetBlockHash(uint256 nextBlockHash)
		{
			_BlockHash = nextBlockHash;
			_Transaction.Insert<byte[], uint256>("BlockHash", BlockHashKey, nextBlockHash);
		}

		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			var task = new Task(() =>
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
							_Transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
						else
							_Transaction.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
					}
					insertedEntities += all.Count;
					_Transaction.Commit();
				}
				PerformanceCounter.AddInsertedEntities(insertedEntities);
			});
			task.Start(_SingleThread);
			return task;
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
			new Task(() =>
			{
				if(_Engine != null)
				{
					_Engine.Dispose();
					_Engine = null;
				}
			}).Start(_SingleThread);
			_SingleThread.WaitFinished();
			if(_SingleThread != null)
			{
				_SingleThread.Dispose();
				_SingleThread = null;
			}
		}
	}
}
