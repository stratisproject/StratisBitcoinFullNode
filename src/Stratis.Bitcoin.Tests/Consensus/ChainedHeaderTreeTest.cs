using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ChainedHeaderTreeTest
    {
        public class TestContext
        {
            public Network Network = Network.RegTest;
            public Mock<IChainedHeaderValidator> ChainedHeaderValidatorMock = new Mock<IChainedHeaderValidator>();
            public Mock<ICheckpoints> CheckpointsMock = new Mock<ICheckpoints>();
            public Mock<IChainState> ChainStateMock = new Mock<IChainState>();
            public ConsensusSettings ConsensusSettings = new ConsensusSettings();

            public ChainedHeaderTree ChainedHeaderTree;

            public ChainedHeaderTree CreateChainedHeaderTree()
            {
                this.ChainedHeaderTree = new ChainedHeaderTree(this.Network, new ExtendedLoggerFactory(), this.ChainedHeaderValidatorMock.Object, this.CheckpointsMock.Object, this.ChainStateMock.Object, this.ConsensusSettings);
                return this.ChainedHeaderTree;
            }

            public ChainedHeader ExtendAChain(int count, ChainedHeader chainedHeader = null)
            {
                ChainedHeader previousHeader = chainedHeader ?? new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GenesisHash, 0);

                for (int i = 0; i < count; i++)
                {
                    BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                    header.HashPrevBlock = previousHeader.HashBlock;
                    header.Bits = previousHeader.Header.Bits - 1000; // just increase difficulty.
                    ChainedHeader newHeader = new ChainedHeader(header, header.GetHash(), previousHeader);
                    previousHeader = newHeader;
                }

                return previousHeader;
            }

            public List<BlockHeader> ChainedHeaderToList(ChainedHeader chainedHeader, int count)
            {
                var list = new List<BlockHeader>();

                ChainedHeader current = chainedHeader;

                for (int i = 0; i < count; i++)
                {
                    list.Add(current.Header);
                    current = current.Previous;
                }

                list.Reverse();

                return list;
            }

            public bool NoDownloadRequested(ConnectedHeaders connectedHeaders)
            {
                Assert.NotNull(connectedHeaders);

                return (connectedHeaders.DownloadTo == null)
                       && (connectedHeaders.DownloadFrom == null);
            }
        }

        [Fact]
        public void ConnectHeaders_HeadersCantConnect_ShouldFail()
        {
            TestContext testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            Assert.Throws<ConnectHeaderException>(() => chainedHeaderTree.ConnectNewHeaders(1, new List<BlockHeader>(new [] { testContext.Network.GetGenesis().Header})));
        }

        [Fact]
        public void ConnectHeaders_NoNewHeadersToConnect_ShouldReturnNothingToDownload()
        {
            var testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            ChainedHeader chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip);

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            ConnectedHeaders connectedHeaders = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);

            Assert.True(testContext.NoDownloadRequested(connectedHeaders));
            Assert.Equal(11, chainedHeaderTree.GetChainedHeadersByHash().Count);
        }

        [Fact]
        public void ConnectHeaders_HeadersFromTwoPeers_ShouldCreateTwoPeerTips()
        {
            var testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            ChainedHeader chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip);

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            ConnectedHeaders connectedHeaders1 = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);
            ConnectedHeaders connectedHeaders2 = chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);

            Assert.Single(chainedHeaderTree.GetPeerIdsByTipHash());
            Assert.Equal(11, chainedHeaderTree.GetChainedHeadersByHash().Count);

            Assert.Equal(3, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.Count);

            Assert.Equal(ChainedHeaderTree.LocalPeerId, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.ElementAt(0));
            Assert.Equal(1, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.ElementAt(1));
            Assert.Equal(2, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.ElementAt(2));

            Assert.True(testContext.NoDownloadRequested(connectedHeaders1));
            Assert.True(testContext.NoDownloadRequested(connectedHeaders2));
        }

        [Fact]
        public void ConnectHeaders_NewAndExistingHeaders_ShouldCreateNewHeaders()
        {
            TestContext testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            var chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip); // initialize the tree with 10 headers
            chainTip.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
            ChainedHeader newChainTip = testContext.ExtendAChain(10, chainTip); // create 10 more headers

            var listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 10);
            var listOfNewHeaders = testContext.ChainedHeaderToList(newChainTip, 10);

            testContext.ChainStateMock.Setup(s => s.ConsensusTip).Returns(chainTip);
            chainTip.BlockValidationState = ValidationState.FullyValidated;

            var connectedHeadersOld = chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);
            var connectedHeadersNew = chainedHeaderTree.ConnectNewHeaders(1, listOfNewHeaders);

            Assert.Equal(21, chainedHeaderTree.GetChainedHeadersByHash().Count);
            Assert.Equal(10, listOfNewHeaders.Count);
            Assert.True(testContext.NoDownloadRequested(connectedHeadersOld));
            Assert.Equal(listOfNewHeaders.Last(), connectedHeadersNew.DownloadTo.Header);
            Assert.Equal(listOfNewHeaders.First(), connectedHeadersNew.DownloadFrom.Header);
        }
    }
}