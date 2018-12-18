using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using NSubstitute.Extensions;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Primitives;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class MaturedBlocksProviderTests
    {
        private readonly IDepositExtractor depositExtractor;

        private readonly ILoggerFactory loggerFactory;

        private IFederationGatewaySettings federationSettings;

        private readonly ILogger logger;

        private readonly IBlockRepository blockRepository;

        private readonly IConsensusManager consensusManager;

        private readonly ConcurrentChain chain;

        public MaturedBlocksProviderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.federationSettings = Substitute.For<IFederationGatewaySettings>();
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.chain = Substitute.ForPartsOf<ConcurrentChain>();
            this.blockRepository = Substitute.For<IBlockRepository>();
            this.consensusManager = Substitute.For<IConsensusManager>();
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

            this.depositExtractor.ExtractBlockDeposits(null).ReturnsForAnyArgs(new MaturedBlockDepositsModel(new MaturedBlockInfoModel(), new List<IDeposit>()));

            var maturedBlocksProvider = new MaturedBlocksProvider(this.loggerFactory, this.chain, this.depositExtractor, this.blockRepository, this.consensusManager);

            List<MaturedBlockDepositsModel> deposits = maturedBlocksProvider.GetMaturedDepositsAsync(0, 10).GetAwaiter().GetResult();

            Assert.Equal(10, deposits.Count);
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
