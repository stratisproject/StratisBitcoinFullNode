using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockStoreBehaviorTest
    {
        private BlockStoreBehavior behavior;
        private Mock<IChainState> chainState;
        private readonly ILoggerFactory loggerFactory;
        private Mock<IConsensusManager> consensusManager;
        private Mock<IBlockStoreQueue> blockStore;

        public BlockStoreBehaviorTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.chainState = new Mock<IChainState>();
            this.consensusManager = new Mock<IConsensusManager>();
            this.blockStore = new Mock<IBlockStoreQueue>();

            this.behavior = new BlockStoreBehavior(this.chainState.Object, this.loggerFactory, this.consensusManager.Object, this.blockStore.Object);
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