﻿using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
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
        public class TestContext
        {
            public Network Network = Network.RegTest;
            public Mock<IChainedHeaderValidator> ChainedHeaderValidatorMock = new Mock<IChainedHeaderValidator>();
            public Mock<ICheckpoints> CheckpointsMock = new Mock<ICheckpoints>();
            public Mock<IChainState> ChainStateMock = new Mock<IChainState>();
            public Mock<IFinalizedBlockHeight> FinalizedBlockMock = new Mock<IFinalizedBlockHeight>();
            public ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(Network.RegTest));

            internal ChainedHeaderTree ChainedHeaderTree;

            internal ChainedHeaderTree CreateChainedHeaderTree()
            {
                this.ChainedHeaderTree = new ChainedHeaderTree(this.Network, new ExtendedLoggerFactory(), this.ChainedHeaderValidatorMock.Object, this.CheckpointsMock.Object, this.ChainStateMock.Object, this.FinalizedBlockMock.Object, this.ConsensusSettings);
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
                    var newHeader = new ChainedHeader(header, header.GetHash(), previousHeader);
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

            public Block GetNewBlock(ChainedHeader previousBlock)
            {
                var nonce = RandomUtils.GetUInt32();
                var newBlock = Block.Load(new byte[999], Network.Main);
                newBlock.AddTransaction(new Transaction());
                newBlock.UpdateMerkleRoot();
                newBlock.Header.Nonce = nonce;
                newBlock.Header.HashPrevBlock = previousBlock.HashBlock;
                return newBlock;
            }
        }

        [Fact]
        public void ConnectHeaders_HeadersCantConnect_ShouldFail()
        {
            var testContext = new TestContext();
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

        /// <summary>
        /// Issue 2 @ Create chained header tree component #1321
        /// Supply headers that we already have and make sure no new ChainedHeaders were created.
        /// </summary>
        [Fact]
        public void ConnectHeaders_NewAndExistingHeaders_ShouldCreateNewHeaders()
        {
            var testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            ChainedHeader chainTip = testContext.ExtendAChain(10);
            chainedHeaderTree.Initialize(chainTip, true); // initialize the tree with 10 headers
            chainTip.BlockDataAvailability = BlockDataAvailabilityState.BlockAvailable;
            ChainedHeader newChainTip = testContext.ExtendAChain(10, chainTip); // create 10 more headers

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 10);
            List<BlockHeader> listOfNewHeaders = testContext.ChainedHeaderToList(newChainTip, 10);

            chainTip.BlockValidationState = ValidationState.FullyValidated;

            ConnectNewHeadersResult connectedHeadersResultOld = chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);
            ConnectNewHeadersResult connectedHeadersResultNew = chainedHeaderTree.ConnectNewHeaders(1, listOfNewHeaders);

            Assert.Equal(21, chainedHeaderTree.GetChainedHeadersByHash().Count);
            Assert.Equal(10, listOfNewHeaders.Count);
            Assert.True(testContext.NoDownloadRequested(connectedHeadersResultOld));
            Assert.Equal(listOfNewHeaders.Last(), connectedHeadersResultNew.DownloadTo.Header);
            Assert.Equal(listOfNewHeaders.First(), connectedHeadersResultNew.DownloadFrom.Header);
        }

        /// <summary>
        /// Issue 3 @ Create chained header tree component #1321
        /// Supply some headers and then supply some more headers.
        /// Make sure that PeerTipsByPeerId is updated and the total amount of items remain the same.
        /// Make sure that PeerIdsByTipHash is updated.
        /// </summary>
        [Fact]
        public void ConnectHeaders_SupplyHeadersThenSupplyMore_Both_Tip_PeerId_Maps_ShouldBeUpdated()
        {
            var testContext = new TestContext();
            ChainedHeaderTree cht = testContext.CreateChainedHeaderTree();
            ChainedHeader chainTip = testContext.ExtendAChain(10);
            cht.Initialize(chainTip, true);
            
            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 10);
            
            cht.ConnectNewHeaders(1, listOfExistingHeaders);

            Dictionary<uint256, HashSet<int>> peerIdsByTipHashBefore = cht.GetPeerIdsByTipHash().ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value));
            Dictionary<int, uint256> peerTipsByPeerIdBefore = cht.GetPeerTipsByPeerId().ToDictionary(entry => entry.Key, entry => new uint256(entry.Value));

            // (of 25 headers) supply last 5 existing and first 10 new
            ChainedHeader newChainTip = testContext.ExtendAChain(15, chainTip);
            List<BlockHeader> listOfNewAndOldHeaders = testContext.ChainedHeaderToList(newChainTip, 25).GetRange(5, 15);

            cht.ConnectNewHeaders(1, listOfNewAndOldHeaders);

            Dictionary<uint256, HashSet<int>> peerIdsByTipHashAfter = cht.GetPeerIdsByTipHash();
            Dictionary<int, uint256> peerTipsByPeerIdAfter = cht.GetPeerTipsByPeerId();

            // Tip # -> peer id map has changed
            Assert.True(peerIdsByTipHashBefore.FirstOrDefault(x => x.Value.Contains(1)).Key !=
                        peerIdsByTipHashAfter.FirstOrDefault(x => x.Value.Contains(1)).Key);

            // Peer id -> tip # map has changed
            Assert.True(peerTipsByPeerIdBefore[1] != peerTipsByPeerIdAfter[1]);

            // reassigning # so amount of items the same
            Assert.True(peerTipsByPeerIdBefore.Values.Count == peerTipsByPeerIdAfter.Values.Count);
        }

        /// <summary>
        /// Issue 5 @ Create chained header tree component #1321
        /// 2 peers should supply headers - one of the peers creates a fork.
        /// Make sure that ChainedBlock is not created more than once for the same header.
        /// Check that next pointers were created correctly.
        /// </summary>
        [Fact]
        public void ConnectHeaders_HeadersFromTwoPeersWithFork_ShouldCreateBlocksForNewHeaders()
        {
            // Setup
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader chainTip = ctx.ExtendAChain(10);
            cht.Initialize(chainTip, true);
            ctx.ChainStateMock.Setup(s => s.ConsensusTip).Returns(chainTip);

            // Default peer is on tip
            Assert.True(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId].Equals(chainTip.HashBlock));

            // Peer 1 forks two blocks back
            BlockHeader forkedBlock = ctx.GetNewBlock(chainTip.Previous.Previous).Header;

            cht.ConnectNewHeaders(1, new List<BlockHeader>() { forkedBlock });

            uint256 peerOneTipHash = cht.GetPeerTipsByPeerId()[1];
            uint256 hashOfForkedBlock = forkedBlock.GetHash();

            ChainedHeader chainedHeaderOfForkedBlock = cht.GetChainedHeadersByHash()[hashOfForkedBlock];
            ChainedHeader fork = chainTip.FindFork(chainedHeaderOfForkedBlock);

            fork.Should().NotBeNull();

            peerOneTipHash.Should().Be(hashOfForkedBlock, "Peer should be on the fork but wasn't");

            // Next pointers were created correctly
            Assert.Equal(2, fork.Next.Count);   // main chain + 1 branch
            Assert.Contains(chainTip.Previous, fork.Next);  // next points -> main chain
            Assert.Contains(chainedHeaderOfForkedBlock, fork.Next);  // next points -> fork
        }

        /// <summary>
        /// Issue 6 @ Create chained header tree component #1321
        /// Make sure checkpoints are off - supply some headers and CHT should return 
        /// a ToDownload array of the same size as the amount of headers.
        /// </summary>
        [Fact]
        public void ConnectHeaders_SupplyHeaders_ToDownloadArraySizeSameAsNumberOfHeaders()
        {
            // Setup
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader chainTip = ctx.ExtendAChain(5);
            cht.Initialize(chainTip, true);
            ctx.ConsensusSettings.UseCheckpoints = false;

            // Checkpoints are off
            Assert.False(ctx.ConsensusSettings.UseCheckpoints);
            ChainedHeader newChainTip = ctx.ExtendAChain(7, chainTip);
            List<BlockHeader> listOfNewBlockHeaders = ctx.ChainedHeaderToList(newChainTip, 7);

            // Peer 1 supplies some headers
            List<BlockHeader> peer1Headers = listOfNewBlockHeaders.GetRange(0,3);
            cht.ConnectNewHeaders(1, peer1Headers);

            // Peer 2 supplies some more headers
            List<BlockHeader> peer2Headers = listOfNewBlockHeaders.GetRange(3, 4);
            ConnectNewHeadersResult connectNewHeadersResult = cht.ConnectNewHeaders(2, peer2Headers);
            ChainedHeader chainedHeaderFrom = connectNewHeadersResult.DownloadFrom;
            ChainedHeader chainedHeaderTo = connectNewHeadersResult.DownloadTo;
            int headersToDownloadCount = chainedHeaderTo.Height - chainedHeaderFrom.Height + 1; // Inclusive
            
            // ToDownload array of the same size as the amount of headers
            Assert.Equal(headersToDownloadCount, peer2Headers.Count);
        }

        /// <summary>
        /// Issue 8 @ Create chained header tree component #1321
        /// We have a chain and peer B is on the best chain.
        /// After that peer C presents an alternative chain with a forkpoint below B's Tip.
        /// After that peer A prolongs C's chain (but sends all headers from the fork point) with a few valid and one invalid.
        /// Make sure that C's chain is not removed - only headers after that.
        /// </summary>
        [Fact]
        public void ConnectHeaders_MultiplePeersWithForks_CorrectTip()
        {
            // Setup
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader chainTip = ctx.ExtendAChain(10);
            cht.Initialize(chainTip, true);

            List<BlockHeader> listOfExistingHeaders = ctx.ChainedHeaderToList(chainTip, 10);
            cht.ConnectNewHeaders(2, listOfExistingHeaders);
            ChainedHeader peerTwoTip = cht.GetChainedHeaderByPeerId(2);

            // Peer B / Peer 2 is on the best chain.
            peerTwoTip.HashBlock.Should().BeEquivalentTo(chainTip.HashBlock);
            peerTwoTip.ChainWork.Should().BeEquivalentTo(chainTip.ChainWork);

            // After that peer C / Peer 3 presents an alternative chain with a forkpoint below B's Tip.
            BlockHeader forkedBlock = ctx.GetNewBlock(chainTip.Previous.Previous).Header;
            cht.ConnectNewHeaders(3, new List<BlockHeader>() { forkedBlock });
            ChainedHeader peerThreeTip = cht.GetChainedHeaderByPeerId(3);
            ChainedHeader fork = chainTip.FindFork(peerThreeTip);
            fork.Height.Should().BeLessThan(peerTwoTip.Height);

            // After that peer A / PEER_ONE prolongs C's chain.
            const int numberOfBlocksToExtend = 7;
            ChainedHeader peerOneTip = ctx.ExtendAChain(numberOfBlocksToExtend, peerThreeTip);
            List<BlockHeader> listOfPeerOnesHeaders = ctx.ChainedHeaderToList(peerOneTip, numberOfBlocksToExtend + 2 /* (forked 2 back) */);

            // Sends all headers from the fork point
            fork.HashBlock.Should().BeEquivalentTo(listOfPeerOnesHeaders[0].GetHash());

            // With few valid and one invalid.
            foreach (var header in listOfPeerOnesHeaders.GetRange(3, 6))
            {
                ctx.ChainStateMock.Setup(x => x.MarkBlockInvalid(header.GetHash(), null));
            }

            cht.ConnectNewHeaders(1, listOfPeerOnesHeaders);

            // Peer A / PEER_ONE's fork should now have the longest chain
            peerOneTip.Height.Should().BeGreaterThan(peerThreeTip.Height);
        }

        /// <summary>
        /// Issue 9 @ Create chained header tree component #1321
        /// Check that everything is always consumed.
        /// </summary>
        [Fact]
        public void ConnectHeaders_MultiplePeers_CheckEverythingIsConsumed()
        {
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader chainTip = ctx.ExtendAChain(11);
            cht.Initialize(chainTip, true);
            List<BlockHeader> listOfNewHeaders = ctx.ChainedHeaderToList(chainTip, 11);

            List<BlockHeader> peer1Headers = listOfNewHeaders.GetRange(0, 6);
            ConnectNewHeadersResult connectedNewHeadersPeer1 = cht.ConnectNewHeaders(1, peer1Headers);

            // 5 unprocessed headers
            Assert.True(chainTip.Height - connectedNewHeadersPeer1.Consumed.Height == 5);

            List<BlockHeader> peer2Headers = listOfNewHeaders.GetRange(6, 5);
            ConnectNewHeadersResult connectedNewHeadersPeer2 = cht.ConnectNewHeaders(2, peer2Headers);

            // All headers consumed
            Assert.True(connectedNewHeadersPeer2.Consumed.Height == chainTip.Height);
        }
    }
}