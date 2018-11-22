using System;
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
using Stratis.Sidechains.Networks;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.FederatedPeg.Tests
{
    public class FederationGatewayControllerTests
    {
        private readonly Network network;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IMaturedBlockReceiver maturedBlockReceiver;

        private readonly ILeaderProvider leaderProvider;

        private ConcurrentChain chain;

        private readonly IMaturedBlockSender maturedBlockSender;

        private readonly IDepositExtractor depositExtractor;

        public FederationGatewayControllerTests()
        {
            this.network = FederatedPegNetwork.NetworksSelector.Regtest();

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.maturedBlockReceiver = Substitute.For<IMaturedBlockReceiver>();
            this.leaderProvider = Substitute.For<ILeaderProvider>();
            this.maturedBlockSender = Substitute.For<IMaturedBlockSender>();
            this.depositExtractor = Substitute.For<IDepositExtractor>();
        }

        [Fact]
        public void ResyncMaturedBlockDeposits_Fails_When_Block_Not_In_Chain()
        {
            this.chain = this.BuildChain(2);

            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.leaderProvider,
                this.chain,
                this.maturedBlockSender,
                this.depositExtractor);

            MaturedBlockModel model = new MaturedBlockModel()
            {
                BlockHash = TestingValues.GetUint256(),
                BlockHeight = TestingValues.GetPositiveInt()
            };

            IActionResult result = controller.ResyncMaturedBlockDeposits(model);

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
        public void ResyncMaturedBlockDeposits_Fails_When_Block_Height_Greater_Than_Minimum_Deposit_Confirmations()
        {
            // Chain header height : 4 
            // 0 - 1 - 2 - 3 - 4
            this.chain = this.BuildChain(5);

            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.leaderProvider,
                this.chain,
                this.maturedBlockSender,
                this.depositExtractor);

            // Back online at block height : 3
            // 0 - 1 - 2 - 3
            ChainedHeader earlierBlock = this.chain.GetBlock(3);

            var model = new MaturedBlockModel()
            {
                BlockHash = earlierBlock.HashBlock,
                BlockHeight = earlierBlock.Height
            };

            // Minimum deposit confirmations : 2
            this.depositExtractor.MinimumDepositConfirmations.Returns((uint)2);

            // Mature height = 2 (Chain header height (4) - Minimum deposit confirmations (2))
            IActionResult result = controller.ResyncMaturedBlockDeposits(model);

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
                e => e.Message == "Block height 3 submitted is not mature enough. Blocks less than a height of 2 can be processed.");
        }

        [Fact]
        public void ResyncMaturedBlockDeposits_Syncs_And_Sends_All_Block_Deposits()
        {
            this.chain = this.BuildChain(10);

            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.leaderProvider,
                this.chain,
                this.maturedBlockSender,
                this.depositExtractor);

            ChainedHeader earlierBlock = this.chain.GetBlock(2);

            var model = new MaturedBlockModel()
            {
                BlockHash = earlierBlock.HashBlock,
                BlockHeight = earlierBlock.Height
            };

            var minConfirmations = 2;
            this.depositExtractor.MinimumDepositConfirmations.Returns((uint)minConfirmations);

            var depositExtractorCallCount = 0;
            this.depositExtractor.When(x => x.ExtractMaturedBlockDeposits(Arg.Any<ChainedHeader>())).Do(info =>
            {
                depositExtractorCallCount++;
            });

            var maturedBlockSenderCallCount = 0;
            this.depositExtractor.When(x => x.ExtractMaturedBlockDeposits(Arg.Any<ChainedHeader>())).Do(info =>
            {
                maturedBlockSenderCallCount++;
            });

            IActionResult result = controller.ResyncMaturedBlockDeposits(model);

            result.Should().BeOfType<OkResult>();

            var expectedCallCount = (this.chain.Height - minConfirmations) - earlierBlock.Height;

            depositExtractorCallCount.Should().Be(expectedCallCount);
            maturedBlockSenderCallCount.Should().Be(expectedCallCount);
        }

        [Fact]
        public void ReceiveCurrentBlockTip_Should_Call_LeaderProdvider_Update()
        {
            var controller = new FederationGatewayController(
                this.loggerFactory,
                this.maturedBlockReceiver,
                this.leaderProvider,
                this.chain,
                this.maturedBlockSender,
                this.depositExtractor);

            var model = new BlockTipModelRequest()
            {
                Hash = TestingValues.GetUint256().ToString(),
                Height = TestingValues.GetPositiveInt()
            };

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
