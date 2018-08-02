using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreBehaviorTest
    {
        private BlockStoreBehavior behavior;
        private Mock<IBlockStoreCache> blockCache;
        private Mock<IChainState> chainState;
        private ConcurrentChain chain;
        private Mock<IBlockRepository> blockRepository;
        private readonly ILoggerFactory loggerFactory;

        public BlockStoreBehaviorTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.chain = new ConcurrentChain(KnownNetworks.StratisMain);
            this.blockRepository = new Mock<IBlockRepository>();
            this.chainState = new Mock<IChainState>();
            this.blockCache = new Mock<IBlockStoreCache>();

            this.behavior = new BlockStoreBehavior(this.chain, this.blockRepository.Object, this.blockCache.Object, this.chainState.Object, this.loggerFactory);
        }

        [Fact]
        public void AnnounceBlocksWithoutBlocksReturns()
        {
            var blocks = new List<ChainedHeader>();

            Task task = this.behavior.AnnounceBlocksAsync(blocks);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Null(this.behavior.AttachedPeer);
        }
    }
}