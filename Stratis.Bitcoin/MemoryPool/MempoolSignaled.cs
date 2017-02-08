using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool
{
    public class MempoolSignaled : SignaleObserve<Block>
	{
		private readonly MempoolManager manager;
		private readonly ConcurrentChain chain;

		public MempoolSignaled(MempoolManager manager, ConcurrentChain chain)
		{
			this.manager = manager;
			this.chain = chain;
		}

		protected override void OnNextCore(Block value)
		{
			var task = this.manager.RemoveForBlock(value, this.chain.GetBlock(value.GetHash()).Height);

			// wait for the mempool code to complete
			// until the signaler becomes async 
			task.Wait();

		}
	}
}
