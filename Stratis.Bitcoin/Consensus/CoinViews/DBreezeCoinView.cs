using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using DBreeze;

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
		}
		public void Initialize()
		{
			DBreeze.Utils.CustomSerializator.ByteArraySerializator = NBitcoinSerialize;
			DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = NBitcoinDeserialize;

			_Tip = new ChainedBlock(_Network.GetGenesis().Header, 0);
			_Engine = new DBreezeEngine(_Folder);
			using(var tx = _Engine.GetTransaction())
			{
				tx.ValuesLazyLoadingIsOn = false;
				foreach(var row in tx.SelectForward<int, BlockHeader>("Chain"))
				{
					_Tip = new ChainedBlock(row.Value, null, _Tip);
				}
			}
		}

		byte[] NBitcoinSerialize(object obj)
		{
			IBitcoinSerializable serializable = obj as IBitcoinSerializable;
			if(serializable != null)
				return serializable.ToBytes();
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
			throw new NotSupportedException();
		}

		ChainedBlock _Tip;
		public override ChainedBlock Tip
		{
			get
			{
				return _Tip;
			}
		}

		static readonly UnspentOutputs[] NoOutputs = new UnspentOutputs[0];
		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			if(txIds.Length == 0)
				return NoOutputs;
			using(StopWatch.Instance.Start(o => PerformanceCounter.AddQueryTime(o)))
			{
				UnspentOutputs[] result = new UnspentOutputs[txIds.Length];
				using(var txx = _Engine.GetTransaction())
				{
					txx.ValuesLazyLoadingIsOn = false;
					int i = 0;
					foreach(var input in txIds)
					{
						PerformanceCounter.AddQueriedEntities(1);
						var coin = txx.Select<byte[], Coins>("Coins", input.ToBytes(false))?.Value;
						result[i++] = coin == null ? null : new UnspentOutputs(input, coin);
					}
				}
				return result;
			}
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			int insertedEntities = 0;
			using(new StopWatch().Start(o => PerformanceCounter.AddInsertTime(o)))
			{
				using(var tx = _Engine.GetTransaction())
				{
					var fork = FindFork(newTip, Tip);
					var currentTip = newTip;
					var blocks = new ChainedBlock[currentTip.Height - fork.Height];
					int i = 0;
					while(currentTip.Height != fork.Height)
					{
						blocks[i++] = currentTip;
						currentTip = currentTip.Previous;
					}
					Array.Reverse(blocks);
					insertedEntities += blocks.Length;
					foreach(var block in blocks)
						tx.Insert("Chain", block.Height, block.Header);
					var all = unspentOutputs.ToList();
					all.Sort(UnspentOutputsComparer.Instance);
					foreach(var coin in all)
					{
						if(coin.IsPrunable)
							tx.RemoveKey("Coins", coin.TransactionId.ToBytes(false));
						else
							tx.Insert("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
					}
					insertedEntities += all.Count;
					tx.Commit();
				}
				_Tip = newTip;
			}
			PerformanceCounter.AddInsertedEntities(insertedEntities);
		}		

		private readonly BackendPerformanceCounter _PerformanceCounter = new BackendPerformanceCounter();
		public BackendPerformanceCounter PerformanceCounter
		{
			get
			{
				return _PerformanceCounter;
			}
		}

		private ChainedBlock FindFork(ChainedBlock newTip, ChainedBlock tip)
		{
			return newTip.FindFork(tip);
		}

		public void Dispose()
		{
			if(_Engine != null)
			{
				_Engine.Dispose();
				_Engine = null;
			}
		}
	}
}
