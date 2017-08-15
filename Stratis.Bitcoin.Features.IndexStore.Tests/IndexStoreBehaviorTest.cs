using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Stratis.Bitcoin.Features.IndexStore;
using Xunit;
using IIndexRepository = Stratis.Bitcoin.Features.IndexStore.IIndexRepository;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreBehaviorTest
    {
		private IndexStoreBehavior behavior;
		private Mock<IIndexStoreCache> indexCache;
		private Mock<IIndexRepository> indexRepository;
		private ConcurrentChain chain;

		public IndexStoreBehaviorTest()
		{
			this.chain = new ConcurrentChain();
			this.indexRepository = new Mock<IIndexRepository>();
			this.indexCache = new Mock<IIndexStoreCache>();

			this.behavior = new IndexStoreBehavior(this.chain, this.indexRepository.Object, this.indexCache.Object, NullLogger.Instance);
		}

		[Fact]
		public void AnnounceBlocksWithoutBlocksReturns_IX()
		{
			List<uint256> blocks = new List<uint256>();			

			var task = this.behavior.AnnounceBlocks(blocks);

			Assert.Equal(TaskStatus.RanToCompletion, task.Status);
			Assert.Null(this.behavior.AttachedNode);
		}

		[Fact]
		public void AnnounceBlocksWithoutAttachedNodeWithoutBlocksReturns_IX()
		{
			List<uint256> blocks = new List<uint256>();
			blocks.Add(new uint256(1254175239823));			

			var task = this.behavior.AnnounceBlocks(blocks);

			Assert.Equal(TaskStatus.RanToCompletion, task.Status);
			Assert.Null(this.behavior.AttachedNode);
		}		
	}
}
