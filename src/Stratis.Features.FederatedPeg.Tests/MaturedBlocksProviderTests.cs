﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using NSubstitute.Core;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class MaturedBlocksProviderTests
    {
        private readonly IDepositExtractor depositExtractor;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IConsensusManager consensusManager;

        public MaturedBlocksProviderTests()
        {
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.consensusManager = Substitute.For<IConsensusManager>();
        }

        [Fact]
        public async Task GetMaturedBlocksAsyncReturnsDeposits()
        {
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10, null, true);

            foreach (ChainedHeader chainedHeader in headers)
                chainedHeader.Block = new Block(chainedHeader.Header);

            var blocks = new List<ChainedHeaderBlock>(headers.Count);
            foreach (ChainedHeader chainedHeader in headers)
                blocks.Add(new ChainedHeaderBlock(chainedHeader.Block, chainedHeader));

            ChainedHeader tip = headers.Last();

            this.consensusManager.GetBlockDataAsync(Arg.Any<uint256>()).Returns(delegate(CallInfo info)
            {
                uint256 hash = (uint256) info[0];
                ChainedHeaderBlock block = blocks.Single(x => x.ChainedHeader.HashBlock == hash);
                return block;
            });

            uint zero = 0;
            this.depositExtractor.MinimumDepositConfirmations.Returns(info => zero);
            this.depositExtractor.ExtractBlockDeposits(null).ReturnsForAnyArgs(new MaturedBlockDepositsModel(new MaturedBlockInfoModel(), new List<IDeposit>()));
            this.consensusManager.Tip.Returns(tip);

            // Makes every block a matured block.
            var maturedBlocksProvider = new MaturedBlocksProvider(this.loggerFactory, this.depositExtractor, this.consensusManager);

            List<MaturedBlockDepositsModel> deposits = await maturedBlocksProvider.GetMaturedDepositsAsync(0, 10);

            // Expect the number of matured deposits to equal the number of blocks.
            Assert.Equal(10, deposits.Count);
        }
    }
}
