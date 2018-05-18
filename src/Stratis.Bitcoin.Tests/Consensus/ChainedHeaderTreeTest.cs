using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;
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

                foreach (int index in Enumerable.Range(1, count))
                {
                    BlockHeader header  = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                    header.HashPrevBlock = previousHeader.HashBlock;
                    header.Bits = previousHeader.Header.Bits - 1000; // just increase difficulty.
                    ChainedHeader newHeader = new ChainedHeader(header, header.GetHash(), previousHeader);
                    previousHeader = newHeader;
                }

                return previousHeader;
            }

            public List<BlockHeader> ChainedHeaderToList(ChainedHeader chainedHeader, int count)
            {
                List<BlockHeader> list = new List<BlockHeader>();

                ChainedHeader current = chainedHeader;
                while (count > 0)
                {
                    list.Add(current.Header);
                    current = current.Previous;
                    count--;
                }

                list.Reverse();

                return list;
            }

            public bool ConnectedHeadersIsEmpty(ConnectedHeaders connectedHeaders)
            {
                Assert.NotNull(connectedHeaders);

                return connectedHeaders.Consumed == null 
                       && connectedHeaders.DownloadTo == null 
                       && connectedHeaders.DownloadFrom == null;
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
            TestContext testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            var chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip);

            var listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            var connectedHeaders = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);

            Assert.True(testContext.ConnectedHeadersIsEmpty(connectedHeaders));
        }

        [Fact]
        public void ConnectHeaders_HeadersFromTwoPeers_ShouldCreateTwoPeerTips()
        {
            TestContext testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            var chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip);

            var listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            var connectedHeaders1 = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);
            var connectedHeaders2 = chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);

            Assert.Single(chainedHeaderTree.GetPeerTipsByHash);
            Assert.Equal(2, chainedHeaderTree.GetPeerTipsByHash.First().Value.Count);
            Assert.Equal(1, chainedHeaderTree.GetPeerTipsByHash.First().Value.First());
            Assert.Equal(2, chainedHeaderTree.GetPeerTipsByHash.First().Value.Last());

            Assert.True(testContext.ConnectedHeadersIsEmpty(connectedHeaders1));
            Assert.True(testContext.ConnectedHeadersIsEmpty(connectedHeaders2));
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

            var listOfExistingHeaders = testContext.ChainedHeaderToList(newChainTip, 10);

            testContext.ChainStateMock.Setup(s => s.ConsensusTip).Returns(chainTip);
            chainTip.BlockValidationState = ValidationState.FullyValidated;

            var connectedHeaders = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);

            Assert.Equal(listOfExistingHeaders.Last(), connectedHeaders.DownloadTo.Header);
            Assert.Equal(listOfExistingHeaders.First(), connectedHeaders.DownloadFrom.Header);
        }
    }
}