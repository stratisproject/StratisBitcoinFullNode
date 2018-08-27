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

        public InitialBlockDownloadTest()
        {
            this.network = KnownNetworks.Main;
            this.consensusSettings = new ConsensusSettings(new NodeSettings(this.network));
            this.checkpoints = new Checkpoints(this.network, this.consensusSettings);
            this.chainState = new ChainState();
        }

        [Fact]
        public void NotInIBDIfChainStateIsNull()
        {
            var blockDownloadState = new InitialBlockDownloadState(null, this.network, this.consensusSettings, this.checkpoints);
            Assert.False(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainTipIsNull()
        {
            this.chainState.ConsensusTip = null;
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfBehindCheckpoint()
        {
            BlockHeader blockHeader = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.chainState.ConsensusTip = new ChainedHeader(blockHeader, uint256.Zero, 1000);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainWorkIsLessThanMinimum()
        {
            BlockHeader blockHeader = this.network.Consensus.ConsensusFactory.CreateBlockHeader();
            this.chainState.ConsensusTip = new ChainedHeader(blockHeader, uint256.Zero, this.checkpoints.GetLastCheckpointHeight() + 1);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, this.network, this.consensusSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }
    }
}