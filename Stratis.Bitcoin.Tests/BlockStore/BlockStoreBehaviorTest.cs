using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockStore
{
    public class BlockStoreBehaviorTest
    {
		private BlockStoreBehavior behavior;
		private Mock<IBlockStoreCache> blockCache;
		private Mock<Bitcoin.BlockStore.IBlockRepository> blockRepository;
		private ConcurrentChain chain;

		public BlockStoreBehaviorTest()
		{
			this.chain = new ConcurrentChain();
			this.blockRepository = new Mock<Bitcoin.BlockStore.IBlockRepository>();
			this.blockCache = new Mock<IBlockStoreCache>();

			this.behavior = new BlockStoreBehavior(this.chain, this.blockRepository.Object, this.blockCache.Object);
		}

		[Fact]
		public void AnnounceBlocksWithoutBlocksReturns()
		{
			List<uint256> blocks = new List<uint256>();			

			var task = this.behavior.AnnounceBlocks(blocks);

			Assert.Equal(task.Status, TaskStatus.RanToCompletion);
			Assert.Null(behavior.AttachedNode);
		}

		[Fact]
		public void AnnounceBlocksWithoutAttachedNodeWithoutBlocksReturns()
		{
			List<uint256> blocks = new List<uint256>();
			blocks.Add(new uint256(1254175239823));			

			var task = this.behavior.AnnounceBlocks(blocks);

			Assert.Equal(task.Status, TaskStatus.RanToCompletion);
			Assert.Null(behavior.AttachedNode);
		}		
	}
}
