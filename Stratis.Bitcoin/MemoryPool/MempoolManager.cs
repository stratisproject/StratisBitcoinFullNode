using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolManager
	{
		public SchedulerPairSession MempoolScheduler { get; }
		public MempoolValidator Validator { get; } // public for testing
		public MempoolOrphans Orphans { get; } // public for testing
		private readonly TxMempool memPool;

		private readonly ConcurrentChain chain;

		public MempoolManager(SchedulerPairSession mempoolScheduler, TxMempool memPool, ConcurrentChain chain, MempoolValidator validator, MempoolOrphans orphans)
		{
			this.MempoolScheduler = mempoolScheduler;
			this.memPool = memPool;
			this.chain = chain;
			this.Orphans = orphans;
			this.Validator = validator;
		}

		public Task<List<uint256>> GetMempoolAsync()
		{
			return this.MempoolScheduler.DoConcurrent(() => this.memPool.MapTx.Keys.ToList());
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
