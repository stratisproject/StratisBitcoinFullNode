using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using DBreeze;

namespace Stratis.Bitcoin.FullNode.Consensus
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

		public override Coins AccessCoins(uint256 txId)
		{
			using(var tx = _Engine.GetTransaction())
			{
				return tx.Select<byte[], Coins>("Coins", txId.ToBytes(false))?.Value;
			}
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
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
					var txIdsEnum = txIds.GetEnumerator();
					var coinsEnum = coins.GetEnumerator();
					List<Tuple<uint256, Coins>> all = new List<Tuple<uint256, Coins>>();
					while(txIdsEnum.MoveNext())
					{
						coinsEnum.MoveNext();
						all.Add(Tuple.Create(txIdsEnum.Current, coinsEnum.Current));
					}
					all.Sort(CoinPairComparer.Instance);
					foreach(var coin in all)
					{
						tx.Insert("Coins", coin.Item1.ToBytes(false), coin.Item2);
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
			var highest = newTip.Height > tip.Height ? newTip : tip;
			var lowest = highest == newTip ? tip : newTip;
			while(lowest.Height != highest.Height)
			{
				highest = highest.Previous;
			}
			while(lowest.HashBlock != highest.HashBlock)
			{
				lowest = lowest.Previous;
				highest = highest.Previous;
			}
			return highest;
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
