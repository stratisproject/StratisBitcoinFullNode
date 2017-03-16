using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolScheduler : AsyncLock
	{ }

	public class MempoolManager
	{
		public MempoolScheduler MempoolScheduler { get; }
		public MempoolValidator Validator { get; } // public for testing
		public MempoolOrphans Orphans { get; } // public for testing
		private readonly TxMempool memPool;

		public DateTimeProvider DateTimeProvider { get; }
		public NodeSettings NodeArgs { get; set; }


		public MempoolManager(MempoolScheduler mempoolScheduler, TxMempool memPool, 
			MempoolValidator validator, MempoolOrphans orphans, DateTimeProvider dateTimeProvider, NodeSettings nodeArgs)
		{
			this.MempoolScheduler = mempoolScheduler;
			this.memPool = memPool;
			this.DateTimeProvider = dateTimeProvider;
			this.NodeArgs = nodeArgs;
			this.Orphans = orphans;
			this.Validator = validator;
		}

		public MempoolPerformanceCounter PerformanceCounter => this.Validator.PerformanceCounter;

		public Task<List<uint256>> GetMempoolAsync()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.MapTx.Keys.ToList());
		}

		public List<TxMempoolInfo> InfoAll()
		{
			// TODO: DepthAndScoreComparator

			return this.memPool.MapTx.DescendantScore.Select(item => new TxMempoolInfo
			{
				Trx = item.Transaction,
				Time = item.Time,
				FeeRate = new FeeRate(item.Fee, (int) item.GetTxSize()),
				FeeDelta = item.ModifiedFee - item.Fee
			}).ToList();
		}

		public TxMempoolInfo Info(uint256 hash)
		{
			var item = this.memPool.MapTx.TryGet(hash);
			return item == null ? null : new TxMempoolInfo
			{
				Trx = item.Transaction,
				Time = item.Time,
				FeeRate = new FeeRate(item.Fee, (int) item.GetTxSize()),
				FeeDelta = item.ModifiedFee - item.Fee
			};
		}

		public Task<List<TxMempoolInfo>> InfoAllAsync()
		{
			return this.MempoolScheduler.ReadAsync(this.InfoAll);

		}
		public Task<TxMempoolInfo> InfoAsync(uint256 hash)
		{
			return this.MempoolScheduler.ReadAsync(() => this.Info(hash));
		}

		public Task<long> MempoolSize()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.Size);
		}

		public Task Clear()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.Clear());
		}

		public Task<long> MempoolDynamicMemoryUsage()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.DynamicMemoryUsage());
		}

		public Task RemoveForBlock(Block block, int blockHeight)
		{
			//if (this.IsInitialBlockDownload)
			//	return Task.CompletedTask;

			return this.MempoolScheduler.WriteAsync(() => this.memPool.RemoveForBlock(block.Transactions, blockHeight));
		}
	}
}
