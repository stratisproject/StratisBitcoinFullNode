using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.BlockStore
{
    [TestClass]
    public class BlockStoreBehaviorTest
    {
		private BlockStoreBehavior behavior;
		private Mock<IBlockStoreCache> blockCache;
		private Mock<Bitcoin.BlockStore.IBlockRepository> blockRepository;
		private ConcurrentChain chain;

        [TestInitialize]
		public void Initialize()
		{
			this.chain = new ConcurrentChain();
			this.blockRepository = new Mock<Bitcoin.BlockStore.IBlockRepository>();
			this.blockCache = new Mock<IBlockStoreCache>();

			this.behavior = new BlockStoreBehavior(this.chain, this.blockRepository.Object, this.blockCache.Object);
		}

		[TestMethod]
		public void AnnounceBlocksWithoutBlocksReturns()
		{
			List<uint256> blocks = new List<uint256>();			

			var task = this.behavior.AnnounceBlocks(blocks);

			Assert.AreEqual(task.Status, TaskStatus.RanToCompletion);
			Assert.IsNull(behavior.AttachedNode);
		}

		[TestMethod]
		public void AnnounceBlocksWithoutAttachedNodeWithoutBlocksReturns()
		{
			List<uint256> blocks = new List<uint256>();
			blocks.Add(new uint256(1254175239823));			

			var task = this.behavior.AnnounceBlocks(blocks);

			Assert.AreEqual(task.Status, TaskStatus.RanToCompletion);
			Assert.IsNull(behavior.AttachedNode);
		}		
	}
}
