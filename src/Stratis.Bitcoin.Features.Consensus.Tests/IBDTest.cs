using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class IBDTest
    {
        private readonly ConsensusSettings consensusSettings;
        private readonly Checkpoints checkpoints;
        private readonly NodeSettings nodeSettings;
        private readonly ChainState chainState;

        public IBDTest()
        {
            this.consensusSettings = new ConsensusSettings(NodeSettings.Default(), new LoggerFactory());
            this.checkpoints = new Checkpoints(Network.Main, this.consensusSettings);
            this.nodeSettings = new NodeSettings("stratis", Network.Main);
            this.chainState = new ChainState(new InvalidBlockHashStore(new DateTimeProvider()));
        }

        [Fact]
        public void NotInIBDIfChainStateIsNull()
        {
            var blockDownloadState = new InitialBlockDownloadState(null, Network.Main, this.nodeSettings, this.checkpoints);
            Assert.False(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainTipIsNull()
        {
            this.chainState.ConsensusTip = null;
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, Network.Main, this.nodeSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfBehindCheckpoint()
        {
            this.chainState.ConsensusTip = new ChainedBlock(new BlockHeader(), uint256.Zero, 1000);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, Network.Main, this.nodeSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }

        [Fact]
        public void InIBDIfChainWorkIsLessThanMinimum()
        {
            this.chainState.ConsensusTip = new ChainedBlock(new BlockHeader(), uint256.Zero, this.checkpoints.GetLastCheckpointHeight() + 1);
            var blockDownloadState = new InitialBlockDownloadState(this.chainState, Network.Main, this.nodeSettings, this.checkpoints);
            Assert.True(blockDownloadState.IsInitialBlockDownload());
        }
    }
}
