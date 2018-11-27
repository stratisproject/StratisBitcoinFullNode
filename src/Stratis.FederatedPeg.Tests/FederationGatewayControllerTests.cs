using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;
using FluentAssertions;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.Tests
{
    public class FederationGatewayControllerTests
    {
        private readonly Network network;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly IMaturedBlocksProvider maturedBlocksProvider;

        private readonly IMaturedBlocksRequester maturedBlocksRequester;

        private readonly ILeaderProvider leaderProvider;

        private ConcurrentChain chain;

        private readonly IDepositExtractor depositExtractor;

        private readonly ILeaderReceiver leaderReceiver;

        public FederationGatewayControllerTests()
        {
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.maturedBlockReceiver = Substitute.For<IMaturedBlockReceiver>();
            this.maturedBlocksRequester = Substitute.For<IMaturedBlocksRequester>();
            this.maturedBlocksProvider = Substitute.For<IMaturedBlocksProvider>();
            this.leaderProvider = Substitute.For<ILeaderProvider>();
            this.depositExtractor = Substitute.For<IDepositExtractor>();
            this.leaderReceiver = Substitute.For<ILeaderReceiver>();
        }

        private MaturedBlocksProvider GetMaturedBlocksProvider()
        {
            IBlockRepository blockRepository = Substitute.For<IBlockRepository>();

            blockRepository.GetBlocksAsync(Arg.Any<List<uint256>>()).ReturnsForAnyArgs((x) =>
            {
                List<uint256> hashes = x.ArgAt<List<uint256>>(0);
                var blocks = new List<Block>();

                foreach (uint256 hash in hashes)
                {
                    blocks.Add(this.network.CreateBlock());
                }

                return blocks;
            });

            return new MaturedBlocksProvider(
                this.loggerFactory,
                this.chain,
                this.depositExtractor,
                blockRepository);
        }

        [Fact]
        public async void GetMaturedBlockDeposits_Fails_When_Block_Not_In_Chain_Async()
        {
            this.chain = Substitute.For<ConcurrentChain>();

            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.maturedBlocksRequester,
                this.leaderProvider,
                this.chain,
                this.GetMaturedBlocksProvider(),
                this.depositExtractor,
                this.leaderReceiver);

            ChainedHeader chainedHeader = this.BuildChain(3).GetBlock(2);
            this.chain.Tip.Returns(chainedHeader);

            IActionResult result = await controller.GetMaturedBlockDepositsAsync(new MaturedBlockRequestModel(1)).ConfigureAwait(false);

            result.Should().BeOfType<ErrorResult>();

            var error = result as ErrorResult;
            error.Should().NotBeNull();

            var errorResponse = error.Value as ErrorResponse;
            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().HaveCount(1);

            errorResponse.Errors.Should().Contain(
                e => e.Status == (int)HttpStatusCode.BadRequest);

            errorResponse.Errors.Should().Contain(
                e => e.Message.Contains("was not found on the block chain"));
        }

        [Fact]
        public async void GetMaturedBlockDeposits_Fails_When_Block_Height_Greater_Than_Minimum_Deposit_Confirmations_Async()
        {
            // Chain header height : 4
            // 0 - 1 - 2 - 3 - 4
            this.chain = this.BuildChain(5);

            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.maturedBlocksRequester,
                this.leaderProvider,
                this.chain,
                this.GetMaturedBlocksProvider(),
                this.depositExtractor,
                this.leaderReceiver);

            // Minimum deposit confirmations : 2
            this.depositExtractor.MinimumDepositConfirmations.Returns((uint)2);

            int maturedHeight = (int)(this.chain.Tip.Height - this.depositExtractor.MinimumDepositConfirmations);

            // Back online at block height : 3
            // 0 - 1 - 2 - 3
            ChainedHeader earlierBlock = this.chain.GetBlock(maturedHeight + 1);

            // Mature height = 2 (Chain header height (4) - Minimum deposit confirmations (2))
            IActionResult result = await controller.GetMaturedBlockDepositsAsync(new MaturedBlockRequestModel(earlierBlock.Height)).ConfigureAwait(false);

            // Block height (3) > Mature height (2) - returns error message
            result.Should().BeOfType<ErrorResult>();

            var error = result as ErrorResult;
            error.Should().NotBeNull();

            var errorResponse = error.Value as ErrorResponse;
            errorResponse.Should().NotBeNull();
            errorResponse.Errors.Should().HaveCount(1);

            errorResponse.Errors.Should().Contain(
                e => e.Status == (int)HttpStatusCode.BadRequest);

            errorResponse.Errors.Should().Contain(
                e => e.Message.Contains($"Block height {earlierBlock.Height} submitted is not mature enough. Blocks less than a height of {maturedHeight} can be processed."));
        }

        [Fact]
        public async void GetMaturedBlockDeposits_Gets_All_Matured_Block_Deposits_Async()
        {
            this.chain = this.BuildChain(10);

            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.maturedBlocksRequester,
                this.leaderProvider,
                this.chain,
                GetMaturedBlocksProvider(),
                this.depositExtractor,
                this.leaderReceiver);

            ChainedHeader earlierBlock = this.chain.GetBlock(2);

            var minConfirmations = 2;
            this.depositExtractor.MinimumDepositConfirmations.Returns((uint)minConfirmations);

            var depositExtractorCallCount = 0;
            this.depositExtractor.ExtractMaturedBlockDeposits(Arg.Any<ChainedHeader>()).Returns(new MaturedBlockDepositsModel(null, null));
            this.depositExtractor.When(x => x.ExtractMaturedBlockDeposits(Arg.Any<ChainedHeader>())).Do(info =>
            {
                depositExtractorCallCount++;
            });

            IActionResult result = await controller.GetMaturedBlockDepositsAsync(new MaturedBlockRequestModel(earlierBlock.Height)).ConfigureAwait(false);

            result.Should().BeOfType<JsonResult>();

            var expectedCallCount = (this.chain.Height - minConfirmations) - earlierBlock.Height;

            depositExtractorCallCount.Should().Be(expectedCallCount);
        }

        [Fact]
        public void ReceiveCurrentBlockTip_Should_Call_LeaderProdvider_Update()
        {
            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.maturedBlocksRequester,
                this.leaderProvider,
                this.chain,
                GetMaturedBlocksProvider(),
                this.depositExtractor,
                this.leaderReceiver);

            var model = new BlockTipModel(TestingValues.GetUint256(), TestingValues.GetPositiveInt(), TestingValues.GetPositiveInt());

            var leaderProviderCallCount = 0;
            this.leaderProvider.When(x => x.Update(Arg.Any<BlockTipModel>())).Do(info =>
            {
                leaderProviderCallCount++;
            });

            IActionResult result = controller.ReceiveCurrentBlockTip(model);

            result.Should().BeOfType<OkResult>();
            leaderProviderCallCount.Should().Be(1);
        }

        private ConcurrentChain BuildChain(int blocks)
        {
            ConcurrentChain chain = new ConcurrentChain(this.network);

            for(int i = 0; i < blocks - 1; i++)
            {
                this.AppendBlock(chain);
            }

            return chain;
        }

        private ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.network.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
        }
    }
}
