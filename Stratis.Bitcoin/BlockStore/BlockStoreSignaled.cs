using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;

namespace Stratis.Bitcoin.MemoryPool
{
    public class BlockStoreSignaled : SignaleObserve<Block>
	{
		private readonly BlockStoreManager manager;
		private readonly ConcurrentChain chain;

		public BlockStoreSignaled(BlockStoreManager manager, ConcurrentChain chain)
		{
			this.manager = manager;
			this.chain = chain;
		}

		protected override void OnNextCore(Block value)
		{
			var unusedstore = this.manager.TryStoreBlock(value, false);

			var task = this.manager.RelayBlock(value.Header.GetHash());
			// wait until the signaler becomes async 
			task.Wait();

		}
	}
}
