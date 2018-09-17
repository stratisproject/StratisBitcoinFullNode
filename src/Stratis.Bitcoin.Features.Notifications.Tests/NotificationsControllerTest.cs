using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Notifications.Controllers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.Features.Notifications.Tests
{
    public class NotificationsControllerTest : LogsTestBase
    {
        private readonly Network network;

        public NotificationsControllerTest()
        {
            this.network = KnownNetworks.StratisMain;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [Trait("Module", "NotificationsController")]
        public void Given_SyncActionIsCalled_When_QueryParameterIsNullOrEmpty_Then_ReturnBadRequest(string from)
        {
            var chain = new Mock<ConcurrentChain>();
            ConsensusManager consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network);

            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, consensusManager, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(from);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ABlockHeightIsSpecified_Then_TheChainIsSyncedFromTheHash()
        {
            // Set up
            int heightLocation = 480946;
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";
            uint256 hash = uint256.Parse(hashLocation);

            var chainedHeader = new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), hash, null);
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(heightLocation)).Returns(chainedHeader);

            ConsensusManager consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, consensusManager, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(heightLocation.ToString());

            // Assert
            chain.Verify(c => c.GetBlock(heightLocation), Times.Once);
            blockNotification.Verify(b => b.SyncFrom(hash), Times.Once);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ABlockHashIsSpecified_Then_TheChainIsSyncedFromTheHash()
        {
            // Set up
            int heightLocation = 480946;
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";
            uint256 hash = uint256.Parse(hashLocation);

            var chainedHeader = new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), hash, null);
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(uint256.Parse(hashLocation))).Returns(chainedHeader);
            ConsensusManager consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network);

            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, consensusManager, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(hashLocation);

            // Assert
            chain.Verify(c => c.GetBlock(heightLocation), Times.Never);
            blockNotification.Verify(b => b.SyncFrom(hash), Times.Once);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ANonExistingBlockHashIsSpecified_Then_ABadRequestErrorIsReturned()
        {
            // Set up
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";

            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(uint256.Parse(hashLocation))).Returns((ChainedHeader)null);
            ConsensusManager consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network);

            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, consensusManager, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            IActionResult result = notificationController.SyncFrom(hashLocation);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_AnInvalidBlockHashIsSpecified_Then_AnExceptionIsThrown()
        {
            // Set up
            string hashLocation = "notAValidHash";
            var chain = new Mock<ConcurrentChain>();
            ConsensusManager consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network);

            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, consensusManager, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            Assert.Throws<FormatException>(() => notificationController.SyncFrom(hashLocation));
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_HeightNotOnChain_Then_ABadRequestErrorIsReturned()
        {
            // Set up
            var chain = new Mock<ConcurrentChain>();
            chain.Setup(c => c.GetBlock(15)).Returns((ChainedHeader)null);
            ConsensusManager consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network);

            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, consensusManager, new Signals.Signals(), new AsyncLoopFactory(new LoggerFactory()), new NodeLifetime());

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            IActionResult result = notificationController.SyncFrom("15");

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }
    }
}
