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
		private readonly TxMempool memPool;
		private readonly ConcurrentChain chain;

		public MempoolManager(SchedulerPairSession mempoolScheduler, TxMempool memPool, ConcurrentChain chain, MempoolValidator validator)
		{
			this.MempoolScheduler = mempoolScheduler;
			this.memPool = memPool;
			this.chain = chain;
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

		public Task<bool> AlreadyHave(Transaction trx)
		{
			// TODO: Implement OrphanTransactions 

			//if (this.chain.Tip()->GetBlockHash() != hashRecentRejectsChainTip)
			//{
			//	// If the chain tip has changed previously rejected transactions
			//	// might be now valid, e.g. due to a nLockTime'd tx becoming valid,
			//	// or a double-spend. Reset the rejects filter and give those
			//	// txs a second chance.
			//	hashRecentRejectsChainTip = chainActive.Tip()->GetBlockHash();
			//	recentRejects->reset();
			//}

			// Use pcoinsTip->HaveCoinsInCache as a quick approximation to exclude
			// requesting or processing some txs which have already been included in a block
			//return recentRejects->contains(inv.hash) ||
			//       mempool.exists(inv.hash) ||
			//       mapOrphanTransactions.count(inv.hash) ||
			//       pcoinsTip->HaveCoinsInCache(inv.hash);

			return this.MempoolScheduler.DoConcurrent(() => this.memPool.Exists(trx.GetHash()));

			// Don't know what it is, just say we already got one

		}

	}
}
