using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class MaturedBlocksProviderTests
    {
        private IDepositExtractor depositExtractor;

        private ILoggerFactory loggerFactory;

        private IFederationGatewaySettings federationSettings;

        private ILogger logger;

        private IBlockRepository blockRepository;

        private ConcurrentChain chain;

        public MaturedBlocksProviderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
            this.blockRepository = Substitute.For<IBlockRepository>();
        }

        [Fact]
        public void GetMaturedBlocksAsyncReturnsDeposits()
        {
            var blocks = new List<Block>();
            uint256 previous = 0;
            for (int i = 0; i < 10; i++)
            {
                blocks.Add(this.ChainedHeaderBuilder(i, previous).Block);
                previous = blocks.Last().GetHash();
            }

            ChainedHeader tip = ChainedHeaderBuilder(10);
            this.chain.Tip.Returns(tip);

            this.blockRepository.GetBlocksAsync(Arg.Any<List<uint256>>()).Returns(blocks);

            var maturedBlocksProvider = new MaturedBlocksProvider(this.loggerFactory, this.chain, this.depositExtractor, this.blockRepository);

            List<IMaturedBlockDeposits> deposits = maturedBlocksProvider.GetMaturedDepositsAsync(0, 10).GetAwaiter().GetResult();

            Assert.Equal(10, deposits.Count);
        }

        private ChainedHeaderBlock ChainHeaderBlockBuilder(int height = 0, uint256 previous = null)
        {
            ChainedHeader chainedHeader = ChainedHeaderBuilder(height);

            return new ChainedHeaderBlock(chainedHeader.Block, chainedHeader);
        }

        private ChainedHeader ChainedHeaderBuilder(int height, uint256 previous = null)
        {
            var chainedHeader = new ChainedHeader(new BlockHeader(), previous ?? uint256.Zero, height);
            this.chain.GetBlock(height).Returns(chainedHeader);
            chainedHeader.Block = new Block();
            return chainedHeader;
        }
    }
}
