﻿using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class InitialBlockDownloadTest
    {
        private readonly ConsensusSettings consensusSettings;
        private readonly Checkpoints checkpoints;
        private readonly BaseSettings baseSettings;
        private readonly ChainState chainState;
        private readonly Network network;

        public InitialBlockDownloadTest()
        {
            this.network = Network.Main;
            var defaults = NodeSettings.Default(network:this.network);
            this.consensusSettings = new ConsensusSettings().Load(defaults);
            this.checkpoints = new Checkpoints(this.network, this.consensusSettings);
            this.baseSettings = new BaseSettings(defaults);
            this.chainState = new ChainState(new InvalidBlockHashStore(DateTimeProvider.Default));
        }

        [Fact]
        public void NotInIBDIfChainStateIsNull()
        {
            var blockDownloadState = new InitialBlockDownloadState(null, this.network, this.baseSettings, this.checkpoints);
            Assert.False(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainTipIsNull()
        {
            this.chainState.ConsensusTip = null;
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.baseSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfBehindCheckpoint()
        {
            this.chainState.ConsensusTip = new ChainedHeader(new BlockHeader(), uint256.Zero, 1000);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.baseSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainWorkIsLessThanMinimum()
        {
            this.chainState.ConsensusTip = new ChainedHeader(new BlockHeader(), uint256.Zero, this.checkpoints.GetLastCheckpointHeight() + 1);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.baseSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }
    }
}
