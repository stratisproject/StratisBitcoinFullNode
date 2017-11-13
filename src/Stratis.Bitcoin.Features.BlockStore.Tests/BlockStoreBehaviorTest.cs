using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreBehaviorTest
    {
        private BlockStoreBehavior behavior;
        private Mock<IBlockStoreCache> blockCache;
        private Mock<IBlockRepository> blockRepository;
        private ConcurrentChain chain;
        private readonly ILoggerFactory loggerFactory;

        public BlockStoreBehaviorTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.chain = new ConcurrentChain();
            this.blockRepository = new Mock<IBlockRepository>();
            this.blockCache = new Mock<IBlockStoreCache>();

            this.behavior = new BlockStoreBehavior(this.chain, this.blockRepository.Object, this.blockCache.Object, this.loggerFactory);
        }

        [Fact]
        public void AnnounceBlocksWithoutBlocksReturns()
        {
            List<uint256> blocks = new List<uint256>();

            var task = this.behavior.AnnounceBlocks(blocks);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Null(this.behavior.AttachedNode);
        }

        [Fact]
        public void AnnounceBlocksWithoutAttachedNodeWithoutBlocksReturns()
        {
            List<uint256> blocks = new List<uint256>();
            blocks.Add(new uint256(1254175239823));

            var task = this.behavior.AnnounceBlocks(blocks);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Null(this.behavior.AttachedNode);
        }
    }
}
