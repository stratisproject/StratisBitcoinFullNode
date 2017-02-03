using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolManager
	{
		public SchedulerPairSession MempoolScheduler { get; }
		public MempoolValidator Validator { get; } // public for testing
		public MempoolOrphans Orphans { get; } // public for testing
		private readonly TxMempool memPool;

		private readonly ConcurrentChain chain;
		private readonly FullNode fullNode;
		public DateTimeProvider DateTimeProvider { get; }
		public NodeArgs NodeArgs { get; set; }

		public MempoolManager(SchedulerPairSession mempoolScheduler, TxMempool memPool, ConcurrentChain chain, 
			MempoolValidator validator, MempoolOrphans orphans, DateTimeProvider dateTimeProvider, NodeArgs nodeArgs, FullNode fullNode)
		{
			this.MempoolScheduler = mempoolScheduler;
			this.memPool = memPool;
			this.chain = chain;
			this.fullNode = fullNode;
			this.DateTimeProvider = dateTimeProvider;
			this.NodeArgs = nodeArgs;
			this.Orphans = orphans;
			this.Validator = validator;
		}

		public Task<List<uint256>> GetMempoolAsync()
		{
			return this.MempoolScheduler.DoConcurrent(() => this.memPool.MapTx.Keys.ToList());
		}
		
		// TODO: how to do this without the fullnode class
		// TODO: use some cache on the IBD value
		public bool IsInitialBlockDownload => this.fullNode.IsInitialBlockDownload();

		public List<TxMempoolInfo> InfoAll()
		{
			// TODO: DepthAndScoreComparator

			return this.memPool.MapTx.DescendantScore.Select(item => new TxMempoolInfo
			{
				tx = item.Transaction,
				nTime = item.Time,
				feeRate = new FeeRate(item.Fee, (int) item.GetTxSize()),
				nFeeDelta = item.ModifiedFee - item.Fee
			}).ToList();
		}

		public TxMempoolInfo Info(uint256 hash)
		{
			var item = this.memPool.MapTx.TryGet(hash);
			return item == null ? null : new TxMempoolInfo
			{
				tx = item.Transaction,
				nTime = item.Time,
				feeRate = new FeeRate(item.Fee, (int) item.GetTxSize()),
				nFeeDelta = item.ModifiedFee - item.Fee
			};
		}

		public Task<List<TxMempoolInfo>> InfoAllAsync()
		{
			return this.MempoolScheduler.DoConcurrent(this.InfoAll);

		}
		public Task<TxMempoolInfo> InfoAsync(uint256 hash)
		{
			return this.MempoolScheduler.DoConcurrent(() => this.Info(hash));
		}

		public Task<long> MempoolSize()
		{
			return this.MempoolScheduler.DoConcurrent(() => this.memPool.Size);
		}

		public Task Clear()
		{
			return this.MempoolScheduler.DoConcurrent(() => this.memPool.Clear());
		}

		public Task<long> MempoolDynamicMemoryUsage()
		{
			return this.MempoolScheduler.DoConcurrent(() => this.memPool.DynamicMemoryUsage());
		}


	}
}
