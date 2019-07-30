using System;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class InitialBlockDownloadTest
    {
        private readonly ConsensusSettings consensusSettings;
        private readonly Checkpoints checkpoints;
        private readonly ChainState chainState;
        private readonly Network network;
        private readonly Mock<ILoggerFactory> loggerFactory;

        public InitialBlockDownloadTest()
        {
            this.network = KnownNetworks.Main;
            this.consensusSettings = new ConsensusSettings(new NodeSettings(this.network));
            this.checkpoints = new Checkpoints(this.network, this.consensusSettings);
            this.chainState = new ChainState();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        }

        [Fact]
        public void NotInIBDIfChainStateIsNull()
        {
            var blockDownloadState = new InitialBlockDownloadState(null, this.network, this.consensusSettings, this.checkpoints, this.loggerFactory.Object, DateTimeProvider.Default);
            Assert.False(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainTipIsNull()
        {
            this.chainState.ConsensusTip = null;
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints, this.loggerFactory.Object, DateTimeProvider.Default);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfBehindCheckpoint()
        {
            BlockHeader blockHeader = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.chainState.ConsensusTip = new ChainedHeader(blockHeader, uint256.Zero, 1000);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints, this.loggerFactory.Object, DateTimeProvider.Default);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainWorkIsLessThanMinimum()
        {
            BlockHeader blockHeader = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.chainState.ConsensusTip = new ChainedHeader(blockHeader, uint256.Zero, this.checkpoints.GetLastCheckpointHeight() + 1);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints, this.loggerFactory.Object, DateTimeProvider.Default);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfTipIsOlderThanMaxAge()
        {
            BlockHeader blockHeader = this.network.Consensus.ConsensusFactory.CreateBlockHeader();

            // Enough work to get us past the chain work check.
            blockHeader.Bits = new Target(new uint256(uint.MaxValue));

            // Block has a time sufficiently in the past that it can't be the tip.
            blockHeader.Time = ((uint) DateTimeOffset.Now.ToUnixTimeSeconds()) - (uint) this.network.MaxTipAge - 1;

            this.chainState.ConsensusTip = new ChainedHeader(blockHeader, uint256.Zero, this.checkpoints.GetLastCheckpointHeight() + 1);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints, this.loggerFactory.Object, DateTimeProvider.Default);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }
    }
}