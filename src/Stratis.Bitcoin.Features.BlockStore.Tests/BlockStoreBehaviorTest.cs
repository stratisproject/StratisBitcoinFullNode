using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreBehaviorTest
    {
        private BlockStoreBehavior behavior;
        private Mock<IBlockStore> blockStore;
        private Mock<IChainState> chainState;
        private ConcurrentChain chain;
        private readonly ILoggerFactory loggerFactory;

        public BlockStoreBehaviorTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.chain = new ConcurrentChain(KnownNetworks.StratisMain);
            this.chainState = new Mock<IChainState>();
            this.blockStore = new Mock<IBlockStore>();

            this.behavior = new BlockStoreBehavior(this.chain, this.blockStore.Object, this.chainState.Object, this.loggerFactory);
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