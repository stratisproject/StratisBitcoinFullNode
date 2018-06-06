using System.Collections.Generic;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ChainedHeaderTreeTest
    {
        private readonly int PEER_DEFAULT = -1;
        private readonly int PEER_ONE = 1;
        private readonly int PEER_TWO = 2;
        private readonly int PEER_THREE = 3;

        public class TestContext
        {
            public Network Network = Network.RegTest;
            public Mock<IChainedHeaderValidator> ChainedHeaderValidatorMock = new Mock<IChainedHeaderValidator>();
            public Mock<ICheckpoints> CheckpointsMock = new Mock<ICheckpoints>();
            public Mock<IChainState> ChainStateMock = new Mock<IChainState>();
            public ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(Network.RegTest));

            internal ChainedHeaderTree ChainedHeaderTree;

            internal ChainedHeaderTree CreateChainedHeaderTree()
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

            public bool NoDownloadRequested(ConnectNewHeadersResult connectNewHeadersResult)
            {
                Assert.NotNull(connectNewHeadersResult);

                return (connectNewHeadersResult.DownloadTo == null)
                       && (connectNewHeadersResult.DownloadFrom == null);
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
            chainedHeaderTree.Initialize(chainTip, true);

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);

            Assert.True(testContext.NoDownloadRequested(connectNewHeadersResult));
            Assert.Equal(11, chainedHeaderTree.GetChainedHeadersByHash().Count);
        }

        [Fact]
        public void ConnectHeaders_HeadersFromTwoPeers_ShouldCreateTwoPeerTips()
        {
            var testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            ChainedHeader chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip, true);

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            ConnectNewHeadersResult connectNewHeaders1 = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);
            ConnectNewHeadersResult connectNewHeaders2 = chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);

            Assert.Single(chainedHeaderTree.GetPeerIdsByTipHash());
            Assert.Equal(11, chainedHeaderTree.GetChainedHeadersByHash().Count);

            Assert.Equal(3, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.Count);

            Assert.Equal(ChainedHeaderTree.LocalPeerId, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.ElementAt(0));
            Assert.Equal(1, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.ElementAt(1));
            Assert.Equal(2, chainedHeaderTree.GetPeerIdsByTipHash().First().Value.ElementAt(2));

            Assert.True(testContext.NoDownloadRequested(connectNewHeaders1));
            Assert.True(testContext.NoDownloadRequested(connectNewHeaders2));
        }

        [Fact]
        public void ConnectHeaders_NewAndExistingHeaders_ShouldCreateNewHeaders()
        {
            TestContext testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            var chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip, true); // initialize the tree with 10 headers
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

        // Supply headers that we already have, make sure no new ChainedHeaders were created.
        [Fact]
        public void ConnectHeaders_SupplyExistingHeaders_DontCreateNewChainHeaders()
        {
            var testContext = new TestContext();
            ChainedHeaderTree cht = testContext.CreateChainedHeaderTree();
            ChainedHeader chainTip = testContext.ExtendAChain(5);
            Assert.Empty(cht.GetChainedHeadersByHash());

            cht.Initialize(chainTip, true);

            Assert.Equal(6, cht.GetChainedHeadersByHash().Keys.Count);

            var beforeKeys = new List<uint256>(cht.GetChainedHeadersByHash().Keys);

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 5);
            cht.ConnectNewHeaders(1, listOfExistingHeaders);
            var afterKeys = new List<uint256>(cht.GetChainedHeadersByHash().Keys);

            // check chainedHeadersByHash map before and after adding duplicate headers
            Assert.Equal(beforeKeys.Count, afterKeys.Count);
            Assert.True(beforeKeys.All(afterKeys.Contains));
        }

        // Supply some headers and after that supply some more headers
        // and make sure that PeerTipsByHash is updated (the total amount of items is the same).
        [Fact]
        public void ConnectHeaders_SupplyHeadersThenSupplyMore_PeerTipsByHashShouldBeUpdated()
        {
            TestContext testContext = new TestContext();
            ChainedHeaderTree cht = testContext.CreateChainedHeaderTree();

            var chainTip = testContext.ExtendAChain(10);
            cht.Initialize(chainTip, true);
            chainTip.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
            ChainedHeader newChainTip = testContext.ExtendAChain(10, chainTip);

            var listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 10);
            var listOfNewHeaders = testContext.ChainedHeaderToList(newChainTip, 10);

            testContext.ChainStateMock.Setup(s => s.ConsensusTip).Returns(chainTip);
            chainTip.BlockValidationState = ValidationState.FullyValidated;

            cht.ConnectNewHeaders(1, listOfExistingHeaders);

            var peerIdsByTipHashBefore = new Dictionary<uint256, HashSet<int>>(cht.GetPeerIdsByTipHash());
            var chainedHeaderTreeHashesBefore = cht.GetChainedHeadersByHash();

            cht.ConnectNewHeaders(1, listOfNewHeaders);

            var peerIdsByTipHashAfter = cht.GetPeerIdsByTipHash();
            var chainedHeaderTreeHashesAfter = cht.GetChainedHeadersByHash();

            // Same number of entries as we are reassigning hash
            Assert.True(chainedHeaderTreeHashesBefore.Count == chainedHeaderTreeHashesAfter.Count );

            // Peer's tip hash has changed
            Assert.True(peerIdsByTipHashBefore.FirstOrDefault(x => x.Value.Contains(1)).Key !=
                        peerIdsByTipHashAfter.FirstOrDefault(x => x.Value.Contains(1)).Key);
        }

        // Make sure checkpoints are off - supply some headers,
        // CHT should return ToDownload array of the same size as the amount of headers.
        [Fact]
        public void ConnectHeaders_SupplyHeaders_ToDownloadArraySizeSameAsNumberOfHeaders()
        {
            // Setup
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            var chainTip = ctx.ExtendAChain(10);
            cht.Initialize(chainTip, true);
            chainTip.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
            ctx.ChainStateMock.Setup(s => s.ConsensusTip).Returns(chainTip);
            ctx.ConsensusSettings.UseCheckpoints = false;
            ChainedHeader newChainTip = ctx.ExtendAChain(10, chainTip);
            List<BlockHeader> listOfNewHeaders = ctx.ChainedHeaderToList(newChainTip, 10);

            // Checkpoints are off
            Assert.False(ctx.ConsensusSettings.UseCheckpoints);

            // Supply some headers
            var connectedNewHeaders = cht.ConnectNewHeaders(PEER_ONE, listOfNewHeaders);

            var chainedHeaderFrom = connectedNewHeaders.DownloadFrom;
            var chainedHeaderTo = connectedNewHeaders.DownloadTo;
            int headersToDownloadCount = chainedHeaderTo.Height - chainedHeaderFrom.Height + 1; // Inclusive

            // ToDownload array of the same size as the amount of headers
            Assert.Equal(headersToDownloadCount, listOfNewHeaders.Count);
        }
    }
}