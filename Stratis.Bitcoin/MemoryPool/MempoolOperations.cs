using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool
{
	/// <summary>
	/// This class contains async methods to call the mempool. 
	/// Calls in to the mem poll will be scheduled asynchronously.
	/// Reads are done concurrently and writes sequentially. 
	/// </summary>
    public class MempoolOperations
	{
		private readonly ConcurrentExclusiveSchedulerPair schedulerPair;
		private readonly TxMemPool pool;

		public MempoolOperations(TxMemPool txMemPool)
		{
			int defaultMaxConcurrencyLevel = Environment.ProcessorCount; // concurrency count
			int defaultMaxitemspertask = 5; // how many exclusive tasks to processes before checking concurrent tasks
			this.schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, defaultMaxConcurrencyLevel, defaultMaxitemspertask);

			this.pool = txMemPool;
		}

		/// <summary>
		/// Queue parallel work
		/// </summary>
		private Task<T> DoParallel<T>(Func<T> run)
		{
			return Task.Factory.StartNew(run, CancellationToken.None, TaskCreationOptions.None, this.schedulerPair.ConcurrentScheduler);
		}

		/// <summary>
		/// Queue scheduled work
		/// </summary>
		private Task<T> DoSchedule<T>(Func<T> run)
		{
			return Task.Factory.StartNew(run, CancellationToken.None, TaskCreationOptions.None, this.schedulerPair.ExclusiveScheduler);
		}

		public Task<long> SizeAsync
		{
			get { return this.DoParallel(() => this.pool.Size); }
		}

		public Task<FeeRate> GetMinFeeAsync(long sizelimit)
		{
			return this.DoParallel(() => this.pool.GetMinFee(sizelimit));
		}

		public Task<bool> AddUncheckedAsync(uint256 hash, TxMemPoolEntry entry, bool validFeeEstimate = true)
		{
			return this.DoSchedule(() => this.pool.AddUnchecked(hash, entry, validFeeEstimate));
		}

		public Task<bool> AddUncheckedAsync(uint256 hash, TxMemPoolEntry entry, TxMemPool.SetEntries setAncestors, bool validFeeEstimate = true)
		{
			return this.DoSchedule(() => this.pool.AddUnchecked(hash, entry, setAncestors, validFeeEstimate));
		}

	}
}
