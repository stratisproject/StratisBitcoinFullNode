using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockStore
{
    public class BlockStore
    {
	    private readonly ConcurrentChain concurrentChain;
	    private readonly BlockRepository blockRepository;
	    private readonly TaskFactory taskFactory;

	    public BlockStore(ConcurrentChain concurrentChain, BlockRepository blockRepository)
	    {
		    this.concurrentChain = concurrentChain;
		    this.blockRepository = blockRepository;
			this.taskFactory = new TaskFactory(new CustomThreadPoolTaskScheduler(2, 500, "BlockStore"));
	    }

	    public Task ProcessGetData(Node node, GetDataPayload getDataPayload)
	    {
		    return this.taskFactory.StartNew(async () =>
		    {
			    foreach (var item in getDataPayload.Inventory.Where(inv => inv.Type == InventoryType.MSG_BLOCK))
			    {
				    var block = await this.blockRepository.GetAsync(item.Hash);

				    if (block != null)
					    node.SendMessage(new BlockPayload(block));
			    }
		    });
	    }

		public Task ProcessGetBlocks(Node node, GetBlocksPayload getBlocksPayload)
		{
			return this.taskFactory.StartNew(() =>
			{
				ChainedBlock chainedBlock = null;
				foreach (var item in getBlocksPayload.BlockLocators.Blocks)
				{
					chainedBlock = this.concurrentChain.GetBlock(item);
					if (chainedBlock != null)
						break;
				}
				
				if (chainedBlock != null)
				{
					var inv = new InvPayload();
					for (var limit = 0; limit < 500; limit++)
					{
						chainedBlock = this.concurrentChain.GetBlock(chainedBlock.Height + 1);
						if (chainedBlock.HashBlock == getBlocksPayload.HashStop)
							break;

						inv.Inventory.Add(new InventoryVector(InventoryType.MSG_BLOCK, chainedBlock.HashBlock));
					}

					if(inv.Inventory.Any())
						node.SendMessage(inv);
				}

				return Task.CompletedTask;
			});
		}
	}
}
