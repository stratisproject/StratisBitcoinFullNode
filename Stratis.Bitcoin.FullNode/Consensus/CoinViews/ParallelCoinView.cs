using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class ParallelCoinView : CoinView, IBackedCoinView
	{
		CoinView _Inner;
		public ParallelCoinView(TaskScheduler taskScheduler, CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			TaskScheduler = taskScheduler;
			_Inner = inner;
			BatchMaxSize = 100;
		}
		public ParallelCoinView(CoinView inner) : this(null, inner)
		{

		}
		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public override ChainedBlock Tip
		{
			get
			{
				return _Inner.Tip;
			}
		}


		TaskScheduler _TaskScheduler;
		public TaskScheduler TaskScheduler
		{
			get
			{
				return _TaskScheduler ?? TaskScheduler.Default;
			}
			set
			{
				_TaskScheduler = value;
			}
		}

		public int BatchMaxSize
		{
			get; set;
		}

		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			int remain;
			int batchCount = Math.DivRem(txIds.Length, BatchMaxSize, out remain);
			if(batchCount == 0)
				return _Inner.FetchCoins(txIds);
			if(remain > 0)
				batchCount++;
			UnspentOutputs[] utxos = new UnspentOutputs[txIds.Length];
			Task[] subfetch = new Task[batchCount];
			int total = txIds.Length;
			int offset = 0;
			for(int i = 0; i < batchCount; i++)
			{
				int localOffset = offset;
				int size = Math.Min(100, total);
				uint256[] txIdsPart = new uint256[size];
				Array.Copy(txIds, offset, txIdsPart, 0, size);
				total -= size;
				offset += size;
				subfetch[i] = new Task(() =>
				{
					Array.Copy(_Inner.FetchCoins(txIdsPart), 0, utxos, localOffset, txIdsPart.Length);
				});
				subfetch[i].Start(TaskScheduler);
			}
			Task.WhenAll(subfetch).GetAwaiter().GetResult();
			return utxos;
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			_Inner.SaveChanges(newTip, unspentOutputs);
		}
	}
}
