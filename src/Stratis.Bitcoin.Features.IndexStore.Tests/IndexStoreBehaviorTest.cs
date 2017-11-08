﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.IndexStore.Tests
{
    public class IndexStoreBehaviorTest
    {
        private IndexStoreBehavior behavior;
        private Mock<IIndexStoreCache> indexCache;
        private Mock<IIndexRepository> indexRepository;
        private ConcurrentChain chain;
        private readonly ILoggerFactory loggerFactory;

        public IndexStoreBehaviorTest()
        {
            this.chain = new ConcurrentChain();
            this.indexRepository = new Mock<IIndexRepository>();
            this.indexCache = new Mock<IIndexStoreCache>();
            this.loggerFactory = new LoggerFactory();

            this.behavior = new IndexStoreBehavior(this.chain, this.indexRepository.Object, this.indexCache.Object, this.loggerFactory);
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