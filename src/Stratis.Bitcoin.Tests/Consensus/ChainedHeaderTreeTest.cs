using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ChainedHeaderTreeTest
    {
        public class CheckpointFixture
        {
            public CheckpointFixture(int height, BlockHeader header)
            {
                if (height < 1) throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater or equal to 1.");

                Guard.NotNull(header, nameof(header));

                this.Height = height;
                this.Header = header;
            }

            public int Height { get; }

            public BlockHeader Header { get; }
        }

        public class InvalidHeaderTestException : ConsensusException
        {
        }

        [Fact]
        public void ConnectHeaders_NoNewHeadersToConnect_ShouldReturnNothingToDownload()
        {
            TestContext testContext = new TestContextBuilder().WithInitialChain(10).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.InitialChainTip;

            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 4);

            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfExistingHeaders);

            Assert.True(testContext.NoDownloadRequested(connectNewHeadersResult));
            Assert.Equal(11, chainedHeaderTree.GetChainedHeadersByHash().Count);
        }

        [Fact]
        public void ConnectHeaders_HeadersFromTwoPeers_ShouldCreateTwoPeerTips()
        {
            TestContext testContext = new TestContextBuilder().WithInitialChain(10).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.InitialChainTip;

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
        /// Issue 1 @ Create chained header tree component #1321
        /// Supply headers where first header can't be connected - should throw.
        /// </summary>
        [Fact]
        public void ConnectHeaders_HeadersCantConnect_ShouldFail()
        {
            TestContext testContext = new TestContextBuilder().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;

            Assert.Throws<ConnectHeaderException>(() => chainedHeaderTree.ConnectNewHeaders(1, new List<BlockHeader>(new[] { testContext.Network.GetGenesis().Header })));
        }

        /// <summary>
        /// Issue 2 @ Create chained header tree component #1321
        /// Supply headers that we already have and make sure no new ChainedHeaders were created.
        /// </summary>
        [Fact]
        public void ConnectHeaders_NewAndExistingHeaders_ShouldCreateNewHeaders()
        {
            TestContext testContext = new TestContextBuilder().WithInitialChain(10).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.InitialChainTip;

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
            Assert.True(connectedHeadersResultNew.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
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
            TestContext testContext = new TestContextBuilder().WithInitialChain(10).Build();
            ChainedHeaderTree cht = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.InitialChainTip;

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
        /// Issue 4 @ Create chained header tree component #1321
        /// Supply headers where half of them are new and half are old.
        /// Make sure that ChainTipToExtand was created for new ones.
        /// </summary>
        [Fact]
        public void ConnectHeaders_HalfOldHalfNew_ShouldCreateHeadersForNew()
        {
            const int initialChainSize = 20, chainExtensionSize = 20;

            // Initialize tree with h1->h20.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.InitialChainTip;

            // Extend chain from h21->h40.
            ChainedHeader newChainTip = testContext.ExtendAChain(chainExtensionSize, chainTip);
            List<BlockHeader> listOfOldAndNewHeaders = testContext.ChainedHeaderToList(newChainTip, initialChainSize + chainExtensionSize);

            // Supply both old and new headers.
            chainedHeaderTree.ConnectNewHeaders(1, listOfOldAndNewHeaders);

            // ChainTipToExtand tree entries are created for all new BlockHeaders.
            IEnumerable<uint256> hashesOfNewBlocks = listOfOldAndNewHeaders.Select(x => x.GetHash()).TakeLast(chainExtensionSize);
            Assert.True(hashesOfNewBlocks.All(x => chainedHeaderTree.GetChainedHeadersByHash().Keys.Contains(x)));
        }

        /// <summary>
        /// Issue 5 @ Create chained header tree component #1321
        /// 3 peers should supply headers - one of the peers creates a fork.
        /// Make sure that ChainedBlock is not created more than once for the same header.
        /// Check that next pointers were created correctly.
        /// </summary>
        [Fact]
        public void ConnectHeaders_HeadersFromTwoPeersWithFork_ShouldCreateBlocksForNewHeaders()
        {
            TestContext testContext = new TestContextBuilder().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.ExtendAChain(7);
            chainedHeaderTree.Initialize(chainTip);
            testContext.ChainState.Setup(s => s.ConsensusTip).Returns(chainTip);

            // Peer 1: 1a - 2a - 3a - 4a - 5a - 6a - 7a - 8a
            Assert.True(chainedHeaderTree.GetChainedHeadersByHash().Count == 8);

            // Peer 2: 1a - 2a - 3a - 4a - 5a - 6a - 7a - 8a
            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 7);
            chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);
            Assert.True(chainedHeaderTree.GetChainedHeadersByHash().Count == 8);

            // Peer 3: 1a - 2a - 3a - 4a - 5b - 6b - 7b - 8b
            var forkedBlockHeader = chainTip.Previous.Previous.Previous.Previous;
            ChainedHeader peerThreeTip = testContext.ExtendAChain(4, forkedBlockHeader);
            List<BlockHeader> listOfNewAndExistingHeaders = testContext.ChainedHeaderToList(peerThreeTip, 6);   // includes common blocks
            chainedHeaderTree.ConnectNewHeaders(3, listOfNewAndExistingHeaders);

            // CHT should contain 12 headers
            Assert.True(chainedHeaderTree.GetChainedHeadersByHash().Count == 12);

            var chainedHeadersByHash = chainedHeaderTree.GetChainedHeadersByHash();

            foreach (int peer in new[] { 2, 3 })
            {
                var nextPointersByHeightMap = new Dictionary<int, List<ChainedHeader>>();

                ChainedHeader chainPointer = chainedHeadersByHash[chainedHeaderTree.GetPeerTipsByPeerId()[peer]];

                // Start at the tip and use previous pointers to traverse the chain down to the Genesis.
                while (chainPointer.Height > 0)
                {
                    // Checking the next pointers.
                    Assert.Contains(chainPointer, chainPointer.Previous.Next);

                    if (!nextPointersByHeightMap.ContainsKey(chainPointer.Height))
                    {
                        nextPointersByHeightMap.Add(chainPointer.Height, new List<ChainedHeader>());
                    }

                    foreach (var nextPtr in chainPointer.Next)
                    {
                        nextPointersByHeightMap[chainPointer.Height].Add(nextPtr);
                    }

                    chainPointer = chainPointer.Previous;
                }

                // Each should have 1 Next pointer.
                Assert.True(nextPointersByHeightMap.Where(x => x.Key < 3 || (x.Key > 3 && x.Key < 7)).All(y => y.Value.Count == 1));

                // Except for 8a and 8b which contain none.
                Assert.True(nextPointersByHeightMap.Where(x => x.Key == 7).All(y => y.Value.Count == 0));

                // And 4a which has 2.
                Assert.True(nextPointersByHeightMap.Where(x => x.Key == 3).All(y => y.Value.Count == 2));
            }

            // Two blocks at each height above the fork.
            Assert.True(chainedHeadersByHash.GroupBy(x => x.Value.Height).Where(x => x.Key > 4).All(y => y.ToList().Count == 2));

            // One block at each height beneath the fork.
            Assert.True(chainedHeadersByHash.GroupBy(x => x.Value.Height).Where(x => x.Key < 4).All(y => y.ToList().Count == 1));
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
            TestContext ctx = new TestContextBuilder().WithInitialChain(5).UseCheckpoints(false).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Checkpoints are off
            Assert.False(ctx.ConsensusSettings.UseCheckpoints);
            ChainedHeader newChainTip = ctx.ExtendAChain(7, chainTip);
            List<BlockHeader> listOfNewBlockHeaders = ctx.ChainedHeaderToList(newChainTip, 7);

            // Peer 1 supplies some headers
            List<BlockHeader> peer1Headers = listOfNewBlockHeaders.GetRange(0, 3);
            cht.ConnectNewHeaders(1, peer1Headers);

            // Peer 2 supplies some more headers
            List<BlockHeader> peer2Headers = listOfNewBlockHeaders.GetRange(3, 4);
            ConnectNewHeadersResult connectNewHeadersResult = cht.ConnectNewHeaders(2, peer2Headers);
            ChainedHeader chainedHeaderFrom = connectNewHeadersResult.DownloadFrom;
            ChainedHeader chainedHeaderTo = connectNewHeadersResult.DownloadTo;
            int headersToDownloadCount = chainedHeaderTo.Height - chainedHeaderFrom.Height + 1; // Inclusive
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // ToDownload array of the same size as the amount of headers
            Assert.Equal(headersToDownloadCount, peer2Headers.Count);
        }

        /// <summary>
        /// Issue 7 @ Create chained header tree component #1321
        /// We have a chain and someone presents an invalid header.
        /// After that our chain's last block shouldn't change, it shouldn't have a valid .Next
        /// and it should throw an exception.
        /// </summary>
        [Fact]
        public void ConnectHeaders_SupplyInvalidHeader_ExistingChainTipShouldNotChange()
        {
            TestContext testContext = new TestContextBuilder().WithInitialChain(5).UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader consensusTip = testContext.InitialChainTip;

            ChainedHeader invalidChainedHeader = testContext.ExtendAChain(1, testContext.InitialChainTip);
            List<BlockHeader> listContainingInvalidHeader = testContext.ChainedHeaderToList(invalidChainedHeader, 1);
            BlockHeader invalidBlockHeader = listContainingInvalidHeader[0];

            testContext.HeaderValidator.Setup(x => x.ValidateHeader(It.Is<ChainedHeader>(y => y.HashBlock == invalidBlockHeader.GetHash()))).Throws(new InvalidHeaderTestException());

            Assert.Throws<InvalidHeaderTestException>(() => chainedHeaderTree.ConnectNewHeaders(1, listContainingInvalidHeader));

            // Chain's last block shouldn't change.
            ChainedHeader consensusTipAfterInvalidHeaderPresented = chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(-1);
            Assert.Equal(consensusTip, consensusTipAfterInvalidHeaderPresented);

            // Last block shouldn't have a Next.
            Assert.Empty(consensusTipAfterInvalidHeaderPresented.Next);
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
            TestContext testContext = new TestContextBuilder().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.ExtendAChain(5);
            chainedHeaderTree.Initialize(chainTip);
            List<BlockHeader> listOfExistingHeaders = testContext.ChainedHeaderToList(chainTip, 5);

            chainedHeaderTree.ConnectNewHeaders(2, listOfExistingHeaders);
            ChainedHeader peerTwoTip = chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(2);

            // Peer 2 is on the best chain.
            // b1 = b2 = b3 = b4 = b5
            peerTwoTip.HashBlock.Should().BeEquivalentTo(chainTip.HashBlock);

            // Peer 3 presents an alternative chain with a forkpoint below Peer 2's Tip.
            // b1 = b2 = b3 = b4 = b5
            //         = c3 = c4 = c5
            ChainedHeader peerThreeTip = testContext.ExtendAChain(3, chainTip.Previous.Previous.Previous);
            List<BlockHeader> listOfNewHeadersFromPeerThree = testContext.ChainedHeaderToList(peerThreeTip, 3);
            chainedHeaderTree.ConnectNewHeaders(3, listOfNewHeadersFromPeerThree);

            ChainedHeader fork = chainTip.FindFork(peerThreeTip);
            fork.Height.Should().BeLessThan(peerTwoTip.Height);

            int oldChainHeaderTreeCount = chainedHeaderTree.GetChainedHeadersByHash().Count;

            // Peer 1 prolongs Peer 3's chain with one invalid block.
            // c3 = c4 = c5 = a6 = |a7| = a8
            const int numberOfBlocksToExtend = 3;
            ChainedHeader peerOneTip = testContext.ExtendAChain(numberOfBlocksToExtend, peerThreeTip);
            List<BlockHeader> listOfPeerOnesHeaders = testContext.ChainedHeaderToList(peerOneTip, numberOfBlocksToExtend + 2 /*include c4=c5*/);

            int depthOfInvalidHeader = 3;
            BlockHeader invalidBlockHeader = listOfPeerOnesHeaders[depthOfInvalidHeader];
            testContext.HeaderValidator.Setup(x =>
                x.ValidateHeader(It.Is<ChainedHeader>(y => y.HashBlock == invalidBlockHeader.GetHash()))).Throws(new InvalidHeaderTestException());

            Assert.Throws<InvalidHeaderTestException>(() => chainedHeaderTree.ConnectNewHeaders(1, listOfPeerOnesHeaders));

            // Headers originally presented by Peer 3 (a4 = a5) have a claim on them and are not removed.
            Assert.True(chainedHeaderTree.GetChainedHeadersByHash().ContainsKey(listOfPeerOnesHeaders[0].GetHash()));
            Assert.True(chainedHeaderTree.GetChainedHeadersByHash().ContainsKey(listOfPeerOnesHeaders[1].GetHash()));

            // The Peer 1 headers before, the invalid header and the header beyond are removed (a6 = a7 = a8).
            Assert.False(chainedHeaderTree.GetChainedHeadersByHash().ContainsKey(listOfPeerOnesHeaders[2].GetHash()));
            Assert.False(chainedHeaderTree.GetChainedHeadersByHash().ContainsKey(listOfPeerOnesHeaders[3].GetHash()));
            Assert.False(chainedHeaderTree.GetChainedHeadersByHash().ContainsKey(listOfPeerOnesHeaders[4].GetHash()));

            Assert.Equal(oldChainHeaderTreeCount, chainedHeaderTree.GetChainedHeadersByHash().Count);

            // Tip claimed by the third peer is in the chain headers by hash structure.
            uint256 tipClaimedByThirdPeer = chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(3).HashBlock;
            Assert.Equal(tipClaimedByThirdPeer, peerThreeTip.HashBlock);

            // C's chain remains at same height after A presents the invalid header.
            Assert.Equal(peerThreeTip.Height, chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(3).Height);
        }

        /// <summary>
        /// Issue 9 @ Create chained header tree component #1321
        /// Check that everything is always consumed.
        /// </summary>
        [Fact]
        public void ConnectHeaders_MultiplePeers_CheckEverythingIsConsumed()
        {
            // Chain A is presented by default peer:
            // h1=h2=h3=h4=h5=h6=h7
            TestContext testContext = new TestContextBuilder().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.ExtendAChain(7);
            chainedHeaderTree.Initialize(chainTip);

            // Chain A is extended by Peer 1:
            // h1=h2=h3=h4=h5=h6=h7=h8=9a=10a=11a=12a=13a.
            ChainedHeader peer1ChainTip = testContext.ExtendAChain(6, chainTip);
            List<BlockHeader> listOfPeer1NewHeaders = testContext.ChainedHeaderToList(peer1ChainTip, 6);
            List<BlockHeader> peer1NewHeaders = listOfPeer1NewHeaders.GetRange(0, 4);

            // 4 headers are processed: 10a=11a=12a=13a
            // 2 are unprocessed: 12a=13a
            ConnectNewHeadersResult connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, peer1NewHeaders);
            Assert.Equal(2, peer1ChainTip.Height - connectedNewHeadersResult.Consumed.Height);

            // Remaining headers are presented.
            connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfPeer1NewHeaders.GetRange(4, 2));

            // All headers are now consumed.
            Assert.Equal(connectedNewHeadersResult.Consumed.HashBlock, peer1ChainTip.HashBlock);

            // Peer 2 presents headers that that are already in the tree: 10a=11a=12a=13a
            // as well as some that are not in it yet: 14a=15a
            ChainedHeader peer2ChainTip = testContext.ExtendAChain(2, peer1ChainTip);
            List<BlockHeader> listOfNewAndExistingHeaders = testContext.ChainedHeaderToList(peer2ChainTip, 6);

            connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(2, listOfNewAndExistingHeaders);
            Assert.Equal(connectedNewHeadersResult.Consumed.HashBlock, peer2ChainTip.HashBlock);

            // Peer 3 presents a list of headers that are already in the tree
            // and the rest of the headers from a new fork to the tree.
            // 10a=11a=12a=13a=14a=15a
            //            =13b=14b=15b
            ChainedHeader peer3ChainTip = testContext.ExtendAChain(3, peer2ChainTip.Previous.Previous.Previous);
            List<BlockHeader> listOfPeer3Headers = testContext.ChainedHeaderToList(peer3ChainTip, 6);
            connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(3, listOfPeer3Headers);

            Assert.Equal(connectedNewHeadersResult.Consumed.HashBlock, peer3ChainTip.HashBlock);

            List<BlockHeader> peerOneChainedHeaderList = testContext.ChainedHeaderToList(peer1ChainTip, 7);
            List<BlockHeader> peerTwoChainedHeaderList = testContext.ChainedHeaderToList(peer2ChainTip, 7);

            // Submit headers that are all already in the tree:
            // Peer 3 supplies all headers from Peer 1.
            connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(3, peerOneChainedHeaderList);
            Assert.Equal(connectedNewHeadersResult.Consumed.HashBlock, chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(3).HashBlock);

            // Peer 3 supplies all headers from Peer 2.
            connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(3, peerTwoChainedHeaderList);
            Assert.Equal(connectedNewHeadersResult.Consumed.HashBlock, chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(3).HashBlock);

            // Peer 4 submits a list of headers in which nothing is already in the tree, forming a fork:
            // 1a=2a=3a=4a=5a=6a
            //         =4c=5c=6c
            ChainedHeader forkPoint = chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(1).GetAncestor(3);  // fork at height 3.
            ChainedHeader peer4ChainTip = testContext.ExtendAChain(3, forkPoint);
            List<BlockHeader> listOfHeaders = testContext.ChainedHeaderToList(peer4ChainTip, 3);
            connectedNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(4, listOfHeaders);

            Assert.Equal(connectedNewHeadersResult.Consumed.HashBlock, chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(4).HashBlock);
        }

        /// <summary>
        /// Issue 10 @ Create chained header tree component #1321
        /// When checkpoints are enabled and the only checkpoint is at block X,
        /// when we present headers before that none are marked for download.
        /// When we first present checkpointed header and some after
        /// all previous are also marked for download & those that are up
        /// to the last checkpoint are marked as assumevalid.
        /// </summary>
        [Fact]
        public void PresentChain_CheckpointsEnabled_MarkToDownloadWhenCheckpointPresented()
        {
            const int initialChainSize = 5;
            const int currentChainExtension = 20;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            ChainedHeader extendedChainTip = testContext.ExtendAChain(currentChainExtension, initialChainTip);

            // Total chain length is h1 -> h25.
            List<BlockHeader> listOfCurrentChainHeaders =
                testContext.ChainedHeaderToList(extendedChainTip, initialChainSize + currentChainExtension);

            // Checkpoints are enabled and the only checkpoint is at h(20).
            const int checkpointHeight = 20;
            var checkpoint = new CheckpointFixture(checkpointHeight, listOfCurrentChainHeaders[checkpointHeight - 1]);
            testContext.SetupCheckpoints(checkpoint);

            // When we present headers before the checkpoint h6 -> h15 none are marked for download.
            int numberOfHeadersBeforeCheckpoint = checkpointHeight - initialChainSize;
            List<BlockHeader> listOfHeadersBeforeCheckpoint = listOfCurrentChainHeaders.GetRange(initialChainSize, numberOfHeadersBeforeCheckpoint - 5);
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfHeadersBeforeCheckpoint);

            // None are marked for download.
            connectNewHeadersResult.DownloadFrom.Should().Be(null);
            connectNewHeadersResult.DownloadTo.Should().Be(null);

            // Check all headers beyond the initial chain (h6 -> h25) have foundation state of header only.
            ValidationState expectedState = ValidationState.HeaderValidated;
            IEnumerable<ChainedHeader> headersBeyondInitialChain = chainedHeaderTree.GetChainedHeadersByHash().Where(x => x.Value.Height > initialChainSize).Select(y => y.Value);
            foreach (ChainedHeader header in headersBeyondInitialChain)
            {
                header.BlockValidationState.Should().Be(expectedState);
                header.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.HeaderOnly);
            }

            // Present remaining headers checkpoint inclusive h16 -> h25.
            // All are marked for download.
            List<BlockHeader> unconsumedHeaders = listOfCurrentChainHeaders.Skip(connectNewHeadersResult.Consumed.Height).ToList();

            connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, unconsumedHeaders);

            connectNewHeadersResult.DownloadFrom.HashBlock.Should().Be(listOfHeadersBeforeCheckpoint.First().GetHash());
            connectNewHeadersResult.DownloadTo.HashBlock.Should().Be(unconsumedHeaders.Last().GetHash());
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            ChainedHeader chainedHeader = chainedHeaderTree.GetChainedHeadersByHash()
                .SingleOrDefault(x => (x.Value.HashBlock == checkpoint.Header.GetHash())).Value;

            // Checking from the checkpoint back to the initialized chain.
            while (chainedHeader.Height > initialChainSize)
            {
                Assert.True(chainedHeader.IsAssumedValid);
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.BlockRequired);
                chainedHeader = chainedHeader.Previous;
            }

            // Checking from the checkpoint forward to the end of the chain.
            chainedHeader = chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(1);
            while (chainedHeader.Height > checkpoint.Height)
            {
                chainedHeader.BlockValidationState.Should().Be(ValidationState.HeaderValidated);
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.BlockRequired);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// Issue 11 @ Create chained header tree component #1321
        /// When checkpoints are enabled and the first is at block X,
        /// when we present headers before that- none marked for download.
        /// When we first present checkpointed header and some after only
        /// headers before the first checkpoint(including it) are marked for download.
        /// </summary>
        [Fact]
        public void PresentChain_CheckpointsEnabled_MarkToDownloadWhenTwoCheckpointsPresented()
        {
            const int initialChainSize = 5;
            const int currentChainExtension = 25;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            ChainedHeader extendedChainTip = testContext.ExtendAChain(currentChainExtension, initialChainTip);

            // Total chain length is h1 -> h30.
            List<BlockHeader> listOfCurrentChainHeaders =
                testContext.ChainedHeaderToList(extendedChainTip, initialChainSize + currentChainExtension);

            // Checkpoints are enabled and there are two checkpoints defined at h(20) and h(30).
            const int checkpointHeight1 = 20;
            const int checkpointHeight2 = 30;
            var checkpoint1 = new CheckpointFixture(checkpointHeight1, listOfCurrentChainHeaders[checkpointHeight1 - 1]);
            var checkpoint2 = new CheckpointFixture(checkpointHeight2, listOfCurrentChainHeaders[checkpointHeight2 - 1]);
            testContext.SetupCheckpoints(checkpoint1, checkpoint2);

            // We present headers before the first checkpoint h6 -> h15.
            int numberOfHeadersBeforeCheckpoint1 = checkpointHeight1 - initialChainSize;
            List<BlockHeader> listOfHeadersBeforeCheckpoint1 =
                listOfCurrentChainHeaders.GetRange(initialChainSize, numberOfHeadersBeforeCheckpoint1 - 5);
            ConnectNewHeadersResult connectNewHeadersResult =
                chainedHeaderTree.ConnectNewHeaders(1, listOfHeadersBeforeCheckpoint1);

            // None are marked for download.
            connectNewHeadersResult.DownloadFrom.Should().Be(null);
            connectNewHeadersResult.DownloadTo.Should().Be(null);

            // Check all headers beyond the initial chain (h6 -> h30) have foundation state of header only.
            ValidationState expectedState = ValidationState.HeaderValidated;
            IEnumerable<ChainedHeader> headersBeyondInitialChain =
                chainedHeaderTree.GetChainedHeadersByHash().Where(x => x.Value.Height > initialChainSize).Select(y => y.Value);
            foreach (ChainedHeader header in headersBeyondInitialChain)
            {
                header.BlockValidationState.Should().Be(expectedState);
            }

            // Present headers h16 -> h29 (including first checkpoint but excluding second).
            List<BlockHeader> unconsumedHeaders = listOfCurrentChainHeaders.Skip(connectNewHeadersResult.Consumed.Height).ToList();
            connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, unconsumedHeaders.SkipLast(1).ToList());

            // Download headers up to including checkpoint1 (h20) but not beyond.
            connectNewHeadersResult.DownloadFrom.HashBlock.Should().Be(listOfHeadersBeforeCheckpoint1.First().GetHash());
            connectNewHeadersResult.DownloadTo.HashBlock.Should().Be(checkpoint1.Header.GetHash());

            // Checking from first checkpoint back to the initialized chain (h20 -> h6).
            ChainedHeader chainedHeader = chainedHeaderTree.GetChainedHeadersByHash()
                .SingleOrDefault(x => (x.Value.HashBlock == checkpoint1.Header.GetHash())).Value;
            while (chainedHeader.Height > initialChainSize)
            {
                Assert.True(chainedHeader.IsAssumedValid);
                chainedHeader = chainedHeader.Previous;
            }

            // Checking from first checkpoint to second checkpoint (h21 -> h29).
            chainedHeader = chainedHeaderTree.GetPeerTipChainedHeaderByPeerId(1);
            while (chainedHeader.Height > checkpoint1.Height)
            {
                chainedHeader.BlockValidationState.Should().Be(ValidationState.HeaderValidated);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// Issue 12 @ Create chained header tree component #1321
        /// Checkpoints are disabled, assumevalid at block X, blocks up to
        /// X-10 are marked for download, blocks before X-20 are fully validated,
        /// headers up to block X + some more are presented, all from X-10
        /// are marked for download. Make sure that all blocks before assumevalid block
        /// that are not fully validated or partially validated are marked assumevalid.
        /// </summary>
        [Fact]
        public void PresentChain_CheckpointsDisabled_BlocksNotFullyOrPartiallyValidatedAreAssumeValid()
        {
            // Checkpoints are disabled.
            // Initial chain has headers (h1-h10).
            const int initialChainOfTenBlocks = 10;
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainOfTenBlocks)
                .UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // Assume valid at Block X (h30).
            const int assumeValidBlockHeight = 30;
            const int extendChainByTwentyBlocks = 20;
            ChainedHeader extendedChainTip = testContext.ExtendAChain(extendChainByTwentyBlocks, initialChainTip);
            Assert.Equal(extendedChainTip.Height, assumeValidBlockHeight);
            testContext.ConsensusSettings.BlockAssumedValid = extendedChainTip.HashBlock;

            // Blocks up to X-10 (h11->h20) are marked for download.
            List<BlockHeader> listOfCurrentChainHeaders =
                testContext.ChainedHeaderToList(extendedChainTip, initialChainOfTenBlocks + extendChainByTwentyBlocks).Take(extendChainByTwentyBlocks).ToList();
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfCurrentChainHeaders);
            ChainedHeader chainedHeaderDownloadFrom = connectNewHeadersResult.DownloadFrom;
            ChainedHeader chainedHeaderDownloadTo = connectNewHeadersResult.DownloadTo;

            chainedHeaderDownloadFrom.HashBlock.Should().Be(initialChainTip.Next[0].HashBlock); // h11
            chainedHeaderDownloadTo.HashBlock.Should().Be(listOfCurrentChainHeaders.Last().GetHash());

            ChainedHeader chainedHeader = chainedHeaderDownloadTo;
            while (chainedHeader.Height >= chainedHeaderDownloadFrom.Height)
            {
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.BlockRequired);
                chainedHeader = chainedHeader.Previous;
            }

            // Blocks before X-20 (h1->h10) are FV.
            chainedHeader = initialChainTip;
            Assert.Equal(chainedHeader.Height, assumeValidBlockHeight - 20);
            ValidationState expectedState = ValidationState.FullyValidated;
            while (chainedHeader.Height > 0)
            {
                chainedHeader.BlockValidationState.Should().Be(expectedState);
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.BlockAvailable);
                chainedHeader = chainedHeader.Previous;
            }

            // Headers up to block X (h30) + some more (h31->h35) are presented.
            const int extendChainByFiveBlocks = 5;
            extendedChainTip = testContext.ExtendAChain(extendChainByFiveBlocks, extendedChainTip);
            listOfCurrentChainHeaders =
                testContext.ChainedHeaderToList(extendedChainTip, assumeValidBlockHeight + extendChainByFiveBlocks);
            connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfCurrentChainHeaders);

            chainedHeader = extendedChainTip;
            while (chainedHeader.Height > assumeValidBlockHeight)
            {
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.HeaderOnly);
                chainedHeader = chainedHeader.Previous;
            }

            // All from X-10 (h21->h30) are marked for download.
            int chainedHeaderHeightXMinus10 = (assumeValidBlockHeight - 10);
            ChainedHeader fromHeader = extendedChainTip.GetAncestor(chainedHeaderHeightXMinus10 + 1);

            chainedHeaderDownloadFrom = connectNewHeadersResult.DownloadFrom;
            chainedHeaderDownloadTo = connectNewHeadersResult.DownloadTo;

            chainedHeaderDownloadFrom.HashBlock.Should().Be(fromHeader.HashBlock);
            chainedHeaderDownloadTo.HashBlock.Should().Be(extendedChainTip.HashBlock);

            // Check block data availability of headers marked for download.
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Make sure that all blocks before assumevalid block
            // that are not fully or partially validated are marked assumevalid.
            ChainedHeader headerBeforeIncludingAssumeValid = chainedHeaderTree
                .GetPeerTipChainedHeaderByPeerId(1).GetAncestor(assumeValidBlockHeight);

            while (headerBeforeIncludingAssumeValid.Height > assumeValidBlockHeight - 20)
            {
                Assert.True(headerBeforeIncludingAssumeValid.IsAssumedValid);
                headerBeforeIncludingAssumeValid = headerBeforeIncludingAssumeValid.Previous;
            }
        }

        /// <summary>
        /// Issue 13 @ Create 2 chains - chain A and chain B, where chain A has more chain work than chain B. Connect both
        /// chains to chain header tree. Consensus tip should be set to chain A. Now extend / update chain B to make it have
        /// more chain work. Attempt to connect chain B again. Consensus tip should be set to chain B.
        /// </summary>
        [Fact]
        public void PresentDifferentChains_AlternativeChainWithMoreChainWorkShouldAlwaysBeMarkedForDownload()
        {
            // Chain header tree setup.
            TestContext ctx = new TestContextBuilder().WithInitialChain(5).UseCheckpoints(false).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Chains A and B setup.
            const int commonChainSize = 4;
            const int chainAExtension = 4;
            const int chainBExtension = 2;
            ChainedHeader commonChainTip = ctx.ExtendAChain(commonChainSize, initialChainTip); // i.e. h1=h2=h3=h4
            ChainedHeader chainATip = ctx.ExtendAChain(chainAExtension, commonChainTip); // i.e. (h1=h2=h3=h4)=a5=a6=a7=a8
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBExtension, commonChainTip); // i.e. (h1=h2=h3=h4)=b5=b6
            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(chainATip, commonChainSize + chainAExtension);
            List<BlockHeader> listOfChainBBlockHeaders = ctx.ChainedHeaderToList(chainBTip, commonChainSize + chainBExtension);

            // Chain A is presented by peer 1. DownloadTo should be chain A tip.
            ConnectNewHeadersResult connectNewHeadersResult = cht.ConnectNewHeaders(1, listOfChainABlockHeaders);
            ChainedHeader chainedHeaderTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderTo.HashBlock.Should().Be(chainATip.HashBlock);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            foreach (ChainedHeader chainedHeader in connectNewHeadersResult.ToArray())
                cht.BlockDataDownloaded(chainedHeader, chainATip.FindAncestorOrSelf(chainedHeader).Block);

            // Set chain A tip as a consensus tip.
            cht.ConsensusTipChanged(chainedHeaderTo);

            // Chain B is presented by peer 2. DownloadTo should be not set, as chain
            // B has less chain work.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            connectNewHeadersResult.DownloadTo.Should().BeNull();

            // Add more chain work and blocks into chain B.
            const int chainBAdditionalBlocks = 4;
            chainBTip = ctx.ExtendAChain(chainBAdditionalBlocks, chainBTip); // i.e. (h1=h2=h3=h4)=b5=b6=b7=b8=b9=b10
            listOfChainBBlockHeaders = ctx.ChainedHeaderToList(chainBTip, commonChainSize + chainBExtension + chainBAdditionalBlocks);
            List<BlockHeader> listOfNewChainBBlockHeaders = listOfChainBBlockHeaders.TakeLast(chainBAdditionalBlocks).ToList();

            // Chain B is presented by peer 2 again.
            // DownloadTo should now be chain B as B has more chain work than chain A.
            // DownloadFrom should be the block where split occurred.
            // h1=h2=h3=h4=(b5)=b6=b7=b8=b9=(b10) - from b5 to b10.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfNewChainBBlockHeaders);

            ChainedHeader chainedHeaderFrom = connectNewHeadersResult.DownloadFrom;
            BlockHeader expectedHeaderFrom = listOfChainBBlockHeaders[commonChainSize];
            chainedHeaderFrom.Header.GetHash().Should().Be(expectedHeaderFrom.GetHash());

            chainedHeaderTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderTo.HashBlock.Should().Be(chainBTip.HashBlock);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
        }

        /// <summary>
        /// Issue 14 @ Chain exists with checkpoints enabled. There are 2 checkpoints. Peer presents a chain that covers
        /// first checkpoint with a prolongation that does not match the 2nd checkpoint. Exception should be thrown and violating headers should be disconnected.
        /// </summary>
        [Fact]
        public void ChainHasTwoCheckPoints_ChainCoveringOnlyFirstCheckPointIsPresented_ChainIsDiscardedUpUntilFirstCheckpoint()
        {
            // Chain header tree setup.
            // Initial chain has 2 headers.
            // Example: h1=h2.
            const int initialChainSize = 2;
            const int currentChainExtension = 6;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            ChainedHeader extendedChainTip = ctx.ExtendAChain(currentChainExtension, initialChainTip); // i.e. h1=h2=h3=h4=h5=h6=h7=h8
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(extendedChainTip, initialChainSize + currentChainExtension);

            // Setup two known checkpoints at header 4 and 7.
            // Example: h1=h2=h3=(h4)=h5=h6=(h7)=h8.
            const int firstCheckpointHeight = 4;
            const int secondCheckpointHeight = 7;
            var checkpoint1 = new CheckpointFixture(firstCheckpointHeight, listOfCurrentChainHeaders[firstCheckpointHeight - 1]);
            var checkpoint2 = new CheckpointFixture(secondCheckpointHeight, listOfCurrentChainHeaders[secondCheckpointHeight - 1]);
            ctx.SetupCheckpoints(checkpoint1, checkpoint2);

            // Setup new chain that only covers first checkpoint but doesn't cover second checkpoint.
            // Example: h1=h2=h3=(h4)=h5=h6=x7=x8=x9=x10.
            const int newChainExtension = 4;
            extendedChainTip = extendedChainTip.GetAncestor(6); // walk back to block 6
            extendedChainTip = ctx.ExtendAChain(newChainExtension, extendedChainTip);
            List<BlockHeader> listOfNewChainHeaders = ctx.ChainedHeaderToList(extendedChainTip, extendedChainTip.Height);

            // First 5 blocks are presented by peer 1.
            // DownloadTo should be set to a checkpoint 1.
            ConnectNewHeadersResult result = cht.ConnectNewHeaders(1, listOfNewChainHeaders.Take(5).ToList());
            result.DownloadTo.HashBlock.Should().Be(checkpoint1.Header.GetHash());
            Assert.True(result.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Remaining 5 blocks are presented by peer 1 which do not cover checkpoint 2.
            // InvalidHeaderException should be thrown.
            List<BlockHeader> violatingHeaders = listOfNewChainHeaders.Skip(5).ToList();
            Action connectAction = () =>
            {
                cht.ConnectNewHeaders(1, violatingHeaders);
            };

            connectAction.Should().Throw<CheckpointMismatchException>();

            // Make sure headers for violating chain don't exist.
            foreach (BlockHeader header in violatingHeaders)
                Assert.False(cht.GetChainedHeadersByHash().ContainsKey(header.GetHash()));
        }

        /// <summary>
        /// Issue 15 @ Checkpoint are disabled. Assume valid is enabled.
        /// Headers that pass assume valid and meet it is presented.
        /// Chain is marked for download.
        /// Alternative chain that is of the same lenght is presented but it doesnt meet the assume valid- also marked as to download.
        /// </summary>
        [Fact]
        public void ChainHasAssumeValidHeaderAndMarkedForDownloadWhenPresented_SecondChainWithoutAssumeValidAlsoMarkedForDownload()
        {
            // Chain header tree setup with disabled checkpoints.
            // Initial chain has 2 headers.
            // Example: h1=h2.
            const int initialChainSize = 2;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Setup two alternative chains A and B of the same length.
            const int presentedChainSize = 4;
            ChainedHeader chainATip = ctx.ExtendAChain(presentedChainSize, initialChainTip); // i.e. h1=h2=a1=a2=a3=a4
            ChainedHeader chainBTip = ctx.ExtendAChain(presentedChainSize, initialChainTip); // i.e. h1=h2=b1=b2=b3=b4
            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(chainATip, initialChainSize + presentedChainSize);
            List<BlockHeader> listOfChainBBlockHeaders = ctx.ChainedHeaderToList(chainBTip, initialChainSize + presentedChainSize);

            // Set "Assume Valid" to the 4th block of the chain A.
            // Example h1=h2=a1=(a2)=a3=a4.
            ctx.ConsensusSettings.BlockAssumedValid = listOfChainABlockHeaders[3].GetHash();

            // Chain A is presented by peer 1. It meets "assume valid" hash and should
            // be marked for a download.
            ConnectNewHeadersResult connectNewHeadersResult = cht.ConnectNewHeaders(1, listOfChainABlockHeaders);
            ChainedHeader chainedHeaderDownloadTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderDownloadTo.HashBlock.Should().Be(chainATip.HashBlock);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Chain B is presented by peer 2. It doesn't meet "assume valid" hash but should still
            // be marked for a download.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            chainedHeaderDownloadTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderDownloadTo.HashBlock.Should().Be(chainBTip.HashBlock);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
        }

        /// <summary>
        /// Issue 16 @ Checkpoints are enabled. After the last checkpoint, there is an assumed valid. The chain
        /// that covers them all is presented - marked for download. After that chain that covers the last checkpoint
        /// but doesn't cover assume valid and is longer is presented - marked for download.
        /// </summary>
        [Fact]
        public void ChainHasOneCheckPointAndAssumeValid_TwoAlternativeChainsArePresented_BothChainsAreMarkedForDownload()
        {
            // Chain header tree setup with disabled checkpoints.
            // Initial chain has 2 headers.
            // Example: h1=h2.
            const int initialChainSize = 2;
            const int extensionChainSize = 2;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Extend chain with 2 more headers.
            initialChainTip = ctx.ExtendAChain(extensionChainSize, initialChainTip); // i.e. h1=h2=h3=h4
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(initialChainTip, initialChainSize + extensionChainSize);

            // Setup a known checkpoint at header 4.
            // Example: h1=h2=h3=(h4).
            const int checkpointHeight = 4;
            var checkpoint = new CheckpointFixture(checkpointHeight, listOfCurrentChainHeaders.Last());
            ctx.SetupCheckpoints(checkpoint);

            // Extend chain and add "Assume valid" at block 6.
            // Example: h1=h2=h3=(h4)=h5=[h6].
            const int chainExtension = 2;
            ChainedHeader extendedChainTip = ctx.ExtendAChain(chainExtension, initialChainTip);
            ctx.ConsensusSettings.BlockAssumedValid = extendedChainTip.HashBlock;

            // Setup two alternative chains A and B. Chain A covers the last checkpoint (4) and "assume valid" (6).
            // Chain B only covers the last checkpoint (4).
            const int chainAExtensionSize = 2;
            const int chainBExtensionSize = 6;
            ChainedHeader chainATip = ctx.ExtendAChain(chainAExtensionSize, extendedChainTip); // i.e. h1=h2=h3=(h4)=h5=[h6]=a7=a8
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBExtensionSize, initialChainTip); // i.e. h1=h2=h3=(h4)=b5=b6=b7=b8=b9=b10
            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(chainATip, initialChainSize + extensionChainSize + chainExtension + chainAExtensionSize);
            List<BlockHeader> listOfChainBBlockHeaders = ctx.ChainedHeaderToList(chainBTip, initialChainSize + extensionChainSize + chainBExtensionSize);

            // Chain A is presented by peer 1.
            // DownloadFrom should be set to header 3.
            // DownloadTo should be set to header 8.
            ConnectNewHeadersResult result = cht.ConnectNewHeaders(1, listOfChainABlockHeaders);
            result.DownloadFrom.HashBlock.Should().Be(listOfChainABlockHeaders.Skip(2).First().GetHash());
            result.DownloadTo.HashBlock.Should().Be(listOfChainABlockHeaders.Last().GetHash());
            Assert.True(result.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Chain B is presented by peer 2.
            // DownloadFrom should be set to header 5.
            // DownloadTo should be set to header 10.
            result = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            result.DownloadFrom.HashBlock.Should().Be(listOfChainBBlockHeaders[checkpointHeight].GetHash());
            result.DownloadTo.HashBlock.Should().Be(listOfChainBBlockHeaders.Last().GetHash());
            Assert.True(result.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
        }

        /// <summary>
        /// Issue 16 @ Checkpoints are enabled. After the last checkpoint, there is an assumed valid. The chain that covers
        /// the last checkpoint but doesn't cover assume valid is presented - marked for download.
        /// </summary>
        [Fact]
        public void ChainHasOneCheckPointAndAssumeValid_ChainsWithCheckpointButMissedAssumeValidIsPresented_BothChainsAreMarkedForDownload()
        {
            // Chain header tree setup with disabled checkpoints.
            // Initial chain has 2 headers.
            // Example: h1=h2.
            const int initialChainSize = 2;
            const int extensionChainSize = 2;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Extend chain with 2 more headers.
            initialChainTip = ctx.ExtendAChain(extensionChainSize, initialChainTip); // i.e. h1=h2=h3=h4
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(initialChainTip, initialChainSize + extensionChainSize);

            // Setup a known checkpoint at header 4.
            // Example: h1=h2=h3=(h4).
            const int checkpointHeight = 4;
            var checkpoint = new CheckpointFixture(checkpointHeight, listOfCurrentChainHeaders.Last());
            ctx.SetupCheckpoints(checkpoint);

            // Extend chain and add "Assume valid" at block 6.
            // Example: h1=h2=h3=(h4)=h5=[h6].
            const int chainExtension = 2;
            ChainedHeader extendedChainTip = ctx.ExtendAChain(chainExtension, initialChainTip);
            ctx.ConsensusSettings.BlockAssumedValid = extendedChainTip.HashBlock;

            // Setup new chain, which covers the last checkpoint (4), but misses "assumed valid".
            const int newChainExtensionSize = 6;
            ChainedHeader newChainTip = ctx.ExtendAChain(newChainExtensionSize, initialChainTip); // i.e. h1=h2=h3=(h4)=b5=b6=b7=b8=b9=b10
            listOfCurrentChainHeaders = ctx.ChainedHeaderToList(newChainTip, initialChainSize + extensionChainSize + newChainExtensionSize);

            // Chain is presented by peer 2.
            // DownloadFrom should be set to header 3.
            // DownloadTo should be set to header 10.
            ConnectNewHeadersResult result = cht.ConnectNewHeaders(2, listOfCurrentChainHeaders);
            result.DownloadFrom.HashBlock.Should().Be(listOfCurrentChainHeaders.Skip(2).First().GetHash());
            result.DownloadTo.HashBlock.Should().Be(listOfCurrentChainHeaders.Last().GetHash());
            Assert.True(result.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
        }

        /// <summary>
        /// Issue 17 @ We advanced consensus tip (CT) and there are some partially validated (PV) headers after the CT.
        /// Now we receive headers that are after the last PV header and include assume valid (AV). Make sure that those
        /// headers that are before the AV header and after the last PV are all marked as AV.
        /// </summary>
        [Fact]
        public void ChainHasPartiallyValidatedAfterConsensusTip_NewHeadersWithAssumeValidPresented_CorrectHeadersAreMarkedAsAssumedValid()
        {
            // Chain header tree setup.
            // Initial chain has 4 headers with the consensus tip at header 4.
            // Example: fv1=fv2=fv3=fv4 (fv - fully validated).
            const int initialChainSize = 4;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend the chain with 2 partially validated headers.
            // Example: fv1=fv2=fv3=(fv4)=pv5=pv6 (pv - partially validated).
            const int partiallyValidatedHeadersCount = 2;
            chainTip = ctx.ExtendAChain(partiallyValidatedHeadersCount, chainTip);

            // Chain is presented by peer 1.
            // Mark pv5 and pv6 as partially validated.
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(chainTip, partiallyValidatedHeadersCount);
            ConnectNewHeadersResult result = cht.ConnectNewHeaders(1, listOfCurrentChainHeaders);
            chainTip = result.Consumed;
            chainTip.BlockValidationState = ValidationState.PartiallyValidated;
            chainTip.Previous.BlockValidationState = ValidationState.PartiallyValidated;

            // Extend the chain with 6 normal headers, where header at the height 9 is an "assumed valid" header.
            // Example: fv1=fv2=fv3=(fv4)=pv5=pv6=h7=h8=(av9)=h10=h11=h12 (av - assumed valid).
            const int extensionHeadersCount = 6;
            chainTip = ctx.ExtendAChain(extensionHeadersCount, chainTip);
            ChainedHeader assumedValidHeader = chainTip.GetAncestor(9);
            ctx.ConsensusSettings.BlockAssumedValid = assumedValidHeader.HashBlock;
            listOfCurrentChainHeaders = ctx.ChainedHeaderToList(chainTip, extensionHeadersCount);

            // Chain is presented by peer 1.
            result = cht.ConnectNewHeaders(1, listOfCurrentChainHeaders);

            // Headers h7-h9 are marked as "assumed valid".
            ChainedHeader consumed = result.Consumed;
            while (consumed.Height > initialChainSize)
            {
                if (consumed.Height == 9)
                    Assert.True(consumed.IsAssumedValid);

                if (consumed.Height == 6)
                    Assert.Equal(ValidationState.PartiallyValidated, consumed.BlockValidationState);

                consumed = consumed.Previous;
            }
        }

        /// <summary>
        /// Issue 21 @ GetChainedHeader called for some bogus block. Should return null because not connected.
        /// </summary>
        [Fact]
        public void GetChainedHeaderCalledForBogusBlock_ResultShouldBeNull()
        {
            // Chain header tree setup. Initial chain has 4 headers.
            // Example: h1=h2=h3=h4.
            const int initialChainSize = 4;
            const int extensionChainSize = 2;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Extend chain with 2 more headers.
            // Example: h1=h2=h3=h4=h5=h6.
            initialChainTip = ctx.ExtendAChain(extensionChainSize, initialChainTip);

            // Call GetChainedHeader on the block from header 6.
            // A null value should be returned.
            ChainedHeader result = cht.GetChainedHeader(initialChainTip.Block.GetHash());
            result.Should().BeNull();
        }

        /// <summary>
        /// Issue 23 @ Block data received (BlockDataDownloaded is called) for already FV block with null pointer.
        /// Nothing should be chained, pointer shouldn't be assigned and result should be false.
        /// </summary>
        [Fact]
        public void BlockDataDownloadedIsCalled_ForFvBlockWithNullPointer_ResultShouldBeFalse()
        {
            // Chain header tree setup. Initial chain has 4 headers with no blocks.
            // Example: h1=h2=h3=h4.
            const int initialChainSize = 4;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize, assignBlocks: false)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Call BlockDataDownloaded and make sure that result is false and nothing is chained.
            Block fakeBlock = ctx.Network.Consensus.ConsensusFactory.CreateBlock();
            bool result = cht.BlockDataDownloaded(initialChainTip, fakeBlock);
            result.Should().BeFalse();
            initialChainTip.Block.Should().BeNull();
        }

        /// <summary>
        /// Issue 24 @ BlockDataDownloaded called for some blocks. Make sure CH.Block is not null and for the
        /// first block true is returned and false for others.
        /// </summary>
        [Fact]
        public void BlockDataDownloadedIsCalled_ForValidBlocksAfterFv_ResultShouldBeTrueForTHeFirstAndFalseForTheRest()
        {
            TestContext ctx = new TestContextBuilder().WithInitialChain(1).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend the chain with 3 headers.
            // Example: h1=h2=h3=h4.
            ChainedHeader newChainTip = ctx.ExtendAChain(3, chainTip);

            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(newChainTip, 3);
            chainTip = cht.ConnectNewHeaders(1, listOfChainABlockHeaders).Consumed;

            // Call BlockDataDownloaded on h2, h3 and h4.
            ChainedHeader chainTip4 = chainTip;
            ChainedHeader chainTip3 = chainTip.Previous;
            ChainedHeader chainTip2 = chainTip3.Previous;
            bool resultForH4 = cht.BlockDataDownloaded(chainTip4, newChainTip.Block);
            bool resultForH3 = cht.BlockDataDownloaded(chainTip3, newChainTip.Previous.Block);
            bool resultForH2 = cht.BlockDataDownloaded(chainTip2, newChainTip.Previous.Previous.Block);

            // Blocks should be set and only header 1 result is true.
            resultForH4.Should().BeFalse();
            chainTip4.Block.Should().NotBeNull();

            resultForH3.Should().BeFalse();
            chainTip3.Block.Should().NotBeNull();

            resultForH2.Should().BeTrue();
            chainTip2.Block.Should().NotBeNull();
        }

        /// <summary>
        /// Issue 25 @ Consensus and headers are at block 5. Block 6 is presented and PV is successful.
        /// Call PartialOrFullValidationFailed and after make sure that there is a single US_CONSTANT on CT.
        /// </summary>
        [Fact]
        public void ConsensusAndHeadersAreAtBlock5_Block6Presented_PartialOrFullValidationFailedCalled_ThereShouldBeASingleConstant()
        {
            // Chain header tree setup. Initial chain has 5 headers.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend a chain by 1 header.
            // Example: h1=h2=h3=h4=h5=h6.
            chainTip = ctx.ExtendAChain(1, chainTip);
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(chainTip, 1);

            // Present header by peer with id 1.
            // Then call PartialOrFullValidationFailed on it, followed by PartialOrFullValidationFailed.
            const int peerId = 1;
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(peerId, listOfCurrentChainHeaders);

            cht.BlockDataDownloaded(connectionResult.Consumed, chainTip.Block);

            cht.PartialValidationSucceeded(connectionResult.Consumed, out bool fullValidationRequired);
            fullValidationRequired.Should().BeTrue();
            cht.PartialOrFullValidationFailed(connectionResult.Consumed);

            // Peer id must be found only once on header 5.
            Dictionary<uint256, HashSet<int>> peerIdsByTipHash = cht.GetPeerIdsByTipHash();
            peerIdsByTipHash.Should().HaveCount(1);
            uint256 header5Hash = connectionResult.Consumed.GetAncestor(5).HashBlock;
            peerIdsByTipHash[header5Hash].Should().HaveCount(1);
            peerIdsByTipHash[header5Hash].Single().Should().Be(ChainedHeaderTree.LocalPeerId);
        }

        /// <summary>
        /// Issue 26 @ CT is at block 5. Call PartialValidationSucceeded with header 6. ConsensusTipChanged on block 6.
        /// Make sure PID moved to 6.
        /// </summary>
        [Fact]
        public void ConsensusAndHeadersAreAtBlock5_Block6Presented_PartialValidationSucceededCalled_LocalPeerIdIsMovedTo6()
        {
            // Chain header tree setup. Initial chain has 5 headers.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend a chain by 1 header.
            // Example: h1=h2=h3=h4=h5=h6.
            chainTip = ctx.ExtendAChain(1, chainTip);
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(chainTip, 1);

            // Present header by peer with id 1 and then call PartialValidationSucceeded on it.
            const int peerId = 1;
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(peerId, listOfCurrentChainHeaders);

            cht.BlockDataDownloaded(connectionResult.Consumed, chainTip.Block);

            cht.PartialValidationSucceeded(connectionResult.Consumed, out bool fullValidationRequired);
            fullValidationRequired.Should().BeTrue();

            // Call ConsensusTipChanged on chaintip at header 6.
            cht.ConsensusTipChanged(chainTip);

            // PID moved to header 6.
            Dictionary<uint256, HashSet<int>> peerIdsByTipHash = cht.GetPeerIdsByTipHash();
            uint256 header5Hash = connectionResult.Consumed.GetAncestor(5).HashBlock;
            uint256 header6Hash = connectionResult.Consumed.HashBlock;

            peerIdsByTipHash.Should().HaveCount(1);
            peerIdsByTipHash.Should().NotContainKey(header5Hash);
            peerIdsByTipHash[header6Hash].Should().HaveCount(2);
            peerIdsByTipHash[header6Hash].Should().Contain(ChainedHeaderTree.LocalPeerId);
            peerIdsByTipHash[header6Hash].Should().Contain(peerId);
        }

        /// <summary>
        /// Issue 27 @  Checkpoints are disabled. Chain tip is at header 5. Present a chain A with headers equal to max reorg of 500 blocks plus extra 50.
        /// Then start syncing until block 490. Peer 2 presents the alternative chain with 2 headers after fork point at header 5. We then you join the rest of
        /// 60 blocks. ConsensusTipChanged should return identifier of the second peer at block number 506.
        /// </summary>
        [Fact]
        public void ChainWithMaxReorgPlusExtraHeadersIsCalled_AnotherChainIsPresented_ConsensusTipChangedReturnsSecondPeerId()
        {
            // Chain header tree setup. Initial chain has 5 headers.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // Extend the chain (chain A) with max reorg headers (500) + 50 extra.
            // Example: h1=h2=h3=h4=(h5)=a6=...=a555.
            const int maxReorg = 500;
            ctx.ChainState.Setup(x => x.MaxReorgLength).Returns(maxReorg);
            ChainedHeader chainATip = ctx.ExtendAChain(maxReorg + 50, initialChainTip);

            // Chain A is presented by peer 1.
            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(chainATip, maxReorg + 50);
            ChainedHeader[] chainAChainHeaders = chainATip.ToArray(maxReorg + 50);
            ChainedHeader consumed = cht.ConnectNewHeaders(1, listOfChainABlockHeaders).Consumed;
            ChainedHeader[] consumedHeaders = consumed.ToArray(maxReorg + 50);

            // Sync 490 blocks from chain A.
            for (int i = 0; i < maxReorg - 10; i++)
            {
                ChainedHeader currentChainTip = consumedHeaders[i];
                Block block = chainAChainHeaders[i].Block;

                cht.BlockDataDownloaded(currentChainTip, block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
                ctx.FinalizedBlockMock.Setup(m => m.GetFinalizedBlockInfo()).Returns(new HashHeightPair(uint256.One, currentChainTip.Height - maxReorg));
                List<int> peerIds = cht.ConsensusTipChanged(currentChainTip);
                peerIds.Should().BeEmpty();
            }

            // Create new chain B with 2 headers after the fork point.
            // Example: h1=h2=h3=h4=(h5)=b7=b8.
            const int chainBExtension = 2;
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBExtension, initialChainTip);

            // Chain B is presented by peer 2.
            List<BlockHeader> listOfChainBHeaders = ctx.ChainedHeaderToList(chainBTip, chainBExtension);
            cht.ConnectNewHeaders(2, listOfChainBHeaders);

            // Continue syncing remaining blocks from chain A.
            for (int i = maxReorg - 10; i < maxReorg + 50; i++)
            {
                ChainedHeader currentChainTip = consumedHeaders[i];
                Block block = chainAChainHeaders[i].Block;

                cht.BlockDataDownloaded(currentChainTip, block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
                ctx.FinalizedBlockMock.Setup(m => m.GetFinalizedBlockInfo()).Returns(new HashHeightPair(uint256.One, currentChainTip.Height - maxReorg));
                List<int> peerIds = cht.ConsensusTipChanged(currentChainTip);
                if (currentChainTip.Height > maxReorg + initialChainSize)
                {
                    peerIds.Should().HaveCount(1);
                    peerIds[0].Should().Be(2);
                }
                else
                {
                    peerIds.Should().BeEmpty();
                }
            }
        }

        /// <summary>
        /// Issue 29 @ Peer presents at least two headers. Those headers will be connected to the tree.
        /// Then we save the first such connected block to variable X and simulate block downloaded for both blocks.
        /// Then the peer is disconnected, which removes its chain from the tree.
        /// Partial validation succeeded is then called on X. No headers to validate should be returned and
        /// full validation required should be false.
        /// </summary>
        [Fact]
        public void ChainedHeaderIsRemovedFromTheTree_PartialValidationSucceededCalled_NothingIsReturned()
        {
            // Chain header tree setup. Initial chain has 5 headers.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chaintip with 2 headers, i.e. h1=h2=h3=h4=h5=h6=h7.
            const int chainExtension = 2;
            chainTip = ctx.ExtendAChain(chainExtension, chainTip);

            // Peer 1 presents these 2 headers.
            const int peer1Id = 1;
            List<BlockHeader> listOfHeaders = ctx.ChainedHeaderToList(chainTip, chainExtension);
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(peer1Id, listOfHeaders);
            ChainedHeader consumedHeader = connectionResult.Consumed;

            // Download both blocks.
            ChainedHeader firstPresentedHeader = consumedHeader.Previous;
            cht.BlockDataDownloaded(consumedHeader, chainTip.Block);
            cht.BlockDataDownloaded(firstPresentedHeader, chainTip.Previous.Block);

            // Disconnect peer 1.
            cht.PeerDisconnected(peer1Id);

            // Attempt to call PartialValidationSucceeded on a saved bock.
            List<ChainedHeaderBlock> headersToValidate = cht.PartialValidationSucceeded(firstPresentedHeader, out bool fullValidationRequired);
            headersToValidate.Should().BeNull();
            fullValidationRequired.Should().BeFalse();
        }

        /// <summary>
        /// Issue 30 @ Chain is 10 headers long. All headers are "Data Available". Call PartialValidationSucceeded on
        /// the first header. First header is marked Partially Validated (PV). Make sure the next header is also marked
        /// for partial validation.
        /// </summary>
        [Fact]
        public void ChainWith10DataAvailableHeaders_PartialValidationSucceededOnTheFirstHeaderCalled_RemainingHeadersPartiallyValidated()
        {
            // Chain header tree setup. Initial chain has 2 headers.
            // Example: h1=h2.
            const int initialChainSize = 2;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chain by 10 headers and connect it to CHT.
            // Example: h1=h2=h3=h4=h5=h6=h7=h8=h9=h10=h11=h12.
            const int extensionSize = 10;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);
            List<BlockHeader> listOfExtendedHeaders = ctx.ChainedHeaderToList(chainTip, extensionSize);
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(1, listOfExtendedHeaders);
            ChainedHeader consumed = connectionResult.Consumed;

            // Download all header blocks and call PartialValidationSucceeded on h3.
            ChainedHeader[] originalHeaderArray = chainTip.ToArray(extensionSize);
            ChainedHeader[] headerArray = consumed.ToArray(extensionSize);
            ChainedHeader firstHeader = headerArray[0];
            ChainedHeader secondHeader = headerArray[1];
            for (int i = 0; i < headerArray.Length; i++)
            {
                cht.BlockDataDownloaded(headerArray[i], originalHeaderArray[i].Block);
            }

            List<ChainedHeaderBlock> listOfHeaders = cht.PartialValidationSucceeded(firstHeader, out bool fullValidationRequired);

            // First header validation state should be "PartiallyValidated" and next header returned.
            firstHeader.BlockValidationState.Should().Be(ValidationState.PartiallyValidated);
            listOfHeaders.Should().HaveCount(1);
            listOfHeaders.First().ChainedHeader.HashBlock.Should().Be(secondHeader.HashBlock);
        }

        /// <summary>
        /// Issue 31 @ Chain is 2 blocks long, CT is header 1, call PartialValidationSucceeded on header 2.
        /// Make sure that full validation is required.
        /// </summary>
        [Fact]
        public void ChainOfHeaders_CallPartialValidationSucceededOnBlockBeyondConsensusTip_FullValidationIsRequired()
        {
            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().Build();

            var initialTip = testContext.ExtendAChain(1);

            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            chainedHeaderTree.Initialize(initialTip);

            ChainedHeader chainTip = testContext.ExtendAChain(1, initialTip);
            List<BlockHeader> listOfChainHeaders = testContext.ChainedHeaderToList(chainTip, 1);

            // Chain is 2 blocks long: h1=h2.
            ConnectNewHeadersResult result = chainedHeaderTree.ConnectNewHeaders(1, listOfChainHeaders);
            result.Consumed.Block = chainTip.Block;
            Assert.NotNull(result.Consumed.Block);

            // Call PartialValidationSucceeded on h2.
            chainedHeaderTree.PartialValidationSucceeded(result.Consumed, out bool fullValidationRequired);

            result.Consumed.BlockValidationState.Should().Be(ValidationState.PartiallyValidated);

            fullValidationRequired.Should().BeTrue();
        }

        /// <summary>
        /// Issue 32 @ Call FullValidationSucceeded on some header.
        /// Make sure header.ValidationState == FV
        /// </summary>
        [Fact]
        public void ChainOfHeaders_CallFullValidationSucceededOnHeader_ValidationStateSetToFullyValidated()
        {
            // Setup with initial chain of 17 headers (h1->h17).
            const int initialChainSize = 17;
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader chainTip = testContext.InitialChainTip;

            // Extend chain and connect all headers (h1->h22).
            const int extensionChainSize = 5;
            chainTip = testContext.ExtendAChain(extensionChainSize, chainTip);
            List<BlockHeader> listOfChainHeaders =
                testContext.ChainedHeaderToList(chainTip, initialChainSize + extensionChainSize);
            ConnectNewHeadersResult connectNewHeadersResult =
                chainedHeaderTree.ConnectNewHeaders(1, listOfChainHeaders);
            chainTip = connectNewHeadersResult.Consumed;

            // Select an arbitrary header h19 on the extended chain.
            ChainedHeader newHeader = chainTip.GetAncestor(19);

            // Verify its initial BlockValidationState.
            Assert.Equal(ValidationState.HeaderValidated, newHeader.BlockValidationState);

            chainedHeaderTree.FullValidationSucceeded(newHeader);
            Assert.Equal(ValidationState.FullyValidated, newHeader.BlockValidationState);
        }

        /// <summary>
        /// Issue 33 @  We receive headers message
        /// (first header in the message is HEADERS_START and last is HEADERS_END).
        /// AV = assume valid header, CP1,CP2 - checkpointed headers. '---' some headers.
        /// HEADERS_START--CP1----AV----HEADERS_END---CP2---
        /// Check that headers till AV(including AV) are marked for download.
        /// Headers after AV are not marked for download.
        /// </summary>
        [Fact]
        public void ConnectHeaders_AssumeValidBetweenTwoCheckPoints_DownloadUpToIncludingAssumeValid()
        {
            const int initialChainSize = 5;
            const int chainExtension = 40;
            const int assumeValidHeaderHeight = 30;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;
            ChainedHeader chainTip = testContext.ExtendAChain(chainExtension, initialChainTip);

            // Assume valid header at h30.
            ChainedHeader assumeValidHeader = chainTip.GetAncestor(assumeValidHeaderHeight);
            testContext.ConsensusSettings.BlockAssumedValid = assumeValidHeader.HashBlock;

            List<BlockHeader> listOfChainHeaders = testContext.ChainedHeaderToList(chainTip, initialChainSize + chainExtension);

            // Two checkpoints at h18 and h45.
            const int firstCheckpointHeight = 18;
            const int secondCheckpointHeight = 45;
            var checkpoint1 = new CheckpointFixture(firstCheckpointHeight, listOfChainHeaders[firstCheckpointHeight - 1]);
            var checkpoint2 = new CheckpointFixture(secondCheckpointHeight, listOfChainHeaders[secondCheckpointHeight - 1]);
            testContext.SetupCheckpoints(checkpoint1, checkpoint2);

            // Present chain h1->h40 covering first checkpoint at h18 and assume valid at h30.
            // Exclude second checkpoint at h45.
            listOfChainHeaders = listOfChainHeaders.Take(40).ToList();
            ConnectNewHeadersResult connectedHeadersResultNew = chainedHeaderTree.ConnectNewHeaders(1, listOfChainHeaders);

            // From initialised chain up to and including assume valid (h6->h30) are marked for download.
            // Headers after assume valid (h31->h40) are not marked for download.
            IEnumerable<BlockHeader> extendedChain = listOfChainHeaders.Skip(initialChainSize);

            Assert.Equal(extendedChain.First(), connectedHeadersResultNew.DownloadFrom.Header);
            Assert.Equal(assumeValidHeader.Header, connectedHeadersResultNew.DownloadTo.Header);

            // Check block data availability of headers marked for download.
            Assert.True(connectedHeadersResultNew.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Check block data availability of headers not marked for download after assumed valid block.
            ChainedHeader chainedHeader = chainTip;
            while (chainedHeader.Height > assumeValidHeader.Height)
            {
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.HeaderOnly);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// Issue 34 @ We receive headers message
        /// (first header in the message is HEADERS_START and last is HEADERS_END).
        /// AV = assume valid header, CP1,CP2 - checkpointed headers. '---' some headers.
        /// CP1---HEADERS_START---AV---HEADERS_END---CP2---
        /// Check that headers till AV(including AV) are marked for download.
        /// Headers after AV are not marked for download.
        /// </summary>
        [Fact]
        public void
        ConnectHeaders_AssumeValidBetweenTwoCheckPoints_BothCheckpointsExcluded_DownloadUpToIncludingAssumeValid()
        {
            const int initialChainSize = 14;
            const int chainExtension = 16;
            const int assumeValidHeaderHeight = 20;
            const int headersStartHeight = 15;
            const int headersEndHeight = 25;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(true).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;
            ChainedHeader chainTip = testContext.ExtendAChain(chainExtension, initialChainTip);

            // Assume valid header at h20.
            ChainedHeader assumeValidChainHeader = chainTip.GetAncestor(assumeValidHeaderHeight);
            testContext.ConsensusSettings.BlockAssumedValid = assumeValidChainHeader.HashBlock;

            List<BlockHeader> listOfChainHeaders = testContext.ChainedHeaderToList(chainTip, initialChainSize + chainExtension);

            // Two checkpoints at h10 and h30.
            const int firstCheckpointHeight = 10;
            const int secondCheckpointHeight = 30;
            var checkpoint1 = new CheckpointFixture(firstCheckpointHeight, listOfChainHeaders[firstCheckpointHeight - 1]);
            var checkpoint2 = new CheckpointFixture(secondCheckpointHeight, listOfChainHeaders[secondCheckpointHeight - 1]);
            testContext.SetupCheckpoints(checkpoint1, checkpoint2);

            // Present chain h15->h25 covering assume valid at h20 and excluding both checkpoints.
            int headersToPresentCount = (headersEndHeight - headersStartHeight + 1 /* inclusive */);
            listOfChainHeaders = listOfChainHeaders.Skip(headersStartHeight - 1).Take(headersToPresentCount).ToList();

            // Present chain:
            // ----CP1----HEADERS_START----AV----HEADERS_END----CP2
            // h1--h10--------h15----------h20------h25---------h30
            ConnectNewHeadersResult connectedHeadersResultNew = chainedHeaderTree.ConnectNewHeaders(1, listOfChainHeaders);

            // From initialised chain up to and including assume valid (h15->h20) inclusive are marked for download.
            // Headers after assume valid (h21->h30) are not marked for download.
            Assert.Equal(listOfChainHeaders.First(), connectedHeadersResultNew.DownloadFrom.Header);
            Assert.Equal(assumeValidChainHeader.Header, connectedHeadersResultNew.DownloadTo.Header);

            // Check block data availability of headers marked for download.
            Assert.True(connectedHeadersResultNew.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Check block data availability of headers not marked for download after assumed valid block.
            ChainedHeader chainedHeader = chainTip;
            while (chainedHeader.Height > assumeValidChainHeader.Height)
            {
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.HeaderOnly);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// Issue 35 @ We receive headers message
        /// (first header in the message is HEADERS_START and last is HEADERS_END).
        /// AV = assume valid header, CP1,CP2 - checkpointed headers. '---' some headers.
        /// LAST_CP = last checkpoint.
        /// ----CP1-----HEADERS_START----LAST_CP----AV-----HEADERS_END
        /// All headers until HEADERS_END (including it) are marked for download.
        /// </summary>
        [Fact]
        public void ConnectHeaders_FirstCheckpointsExcluded_AssumeValidBeyondLastCheckPoint_DownloadAllHeaders()
        {
            const int initialChainSize = 14;
            const int chainExtension = 16;
            const int assumeValidHeaderHeight = 25;
            const int headersStartHeight = 15;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(true).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;
            ChainedHeader chainTip = testContext.ExtendAChain(chainExtension, initialChainTip);

            // Assume valid header at h25.
            ChainedHeader assumeValidChainHeader = chainTip.GetAncestor(assumeValidHeaderHeight);
            testContext.ConsensusSettings.BlockAssumedValid = assumeValidChainHeader.HashBlock;

            List<BlockHeader> listOfChainHeaders =
                testContext.ChainedHeaderToList(chainTip, initialChainSize + chainExtension);

            // Two checkpoints at h10 and h20.
            const int firstCheckpointHeight = 10;
            const int secondCheckpointHeight = 20;
            var checkpoint1 = new CheckpointFixture(firstCheckpointHeight, listOfChainHeaders[firstCheckpointHeight - 1]);
            var checkpoint2 = new CheckpointFixture(secondCheckpointHeight, listOfChainHeaders[secondCheckpointHeight - 1]);
            testContext.SetupCheckpoints(checkpoint1, checkpoint2);

            // Present chain h15->h30 covering second checkpoint at h20 and assume valid at h25.
            // -----CP1----HEADERS_START----CP2-----AV----HEADERS_END
            // h1---h10--------h15----------h20-----h25------h30-----
            listOfChainHeaders = listOfChainHeaders.Skip(headersStartHeight - 1).ToList();
            ConnectNewHeadersResult connectedHeadersResultNew = chainedHeaderTree.ConnectNewHeaders(1, listOfChainHeaders);

            // From initialised chain up to and including headers end (h30) inclusive are marked for download.
            Assert.Equal(listOfChainHeaders.First(), connectedHeadersResultNew.DownloadFrom.Header);
            Assert.Equal(listOfChainHeaders.Last(), connectedHeadersResultNew.DownloadTo.Header);

            // Check block data availability of headers marked for download.
            Assert.True(connectedHeadersResultNew.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
        }

        /// <summary>
        /// Issue 36 @ The list of headers is presented where the 1st half of them can be connected but then there is
        /// header which is not consecutive – its previous hash is not hash of the previous header in the list.
        /// The 1st nonconsecutive header should be header that we saw before, so that it actually connects but it
        /// is out of order.
        /// </summary>
        [Fact]
        public void ListWithOnlyHalfConnectableHeadersPresented_TheFirstNonconsecutiveHeaderShouldBeHeaderWeSawBefore()
        {
            // Chain header tree setup. Initial chain has 2 headers.
            // Example: h1=h2.
            const int initialChainSize = 2;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chain with 10 more headers.
            // Example: h1=h2=a3=a4=a5=a6=a7=a8=a9=a10=a11=a12.
            // Then swap h8 with h2 before connecting it.
            const int extensionChainSize = 10;
            chainTip = ctx.ExtendAChain(extensionChainSize, chainTip);
            List<BlockHeader> listOfHeaders = ctx.ChainedHeaderToList(chainTip, extensionChainSize);
            listOfHeaders[initialChainSize + 5] = chainTip.GetAncestor(2).Header;

            // Present headers that contain out of order header.
            Action connectHeadersAction = () =>
            {
                cht.ConnectNewHeaders(1, listOfHeaders);
            };

            // Exception is thrown and no new headers are connected.
            ChainedHeader[] allHeaders = chainTip.ToArray(initialChainSize + extensionChainSize + 1);
            connectHeadersAction.Should().Throw<ArgumentException>();
            Dictionary<uint256, ChainedHeader> currentHeaders = cht.GetChainedHeadersByHash();
            currentHeaders.Should().HaveCount(initialChainSize + 1); // initial chain size + genesis.
            currentHeaders.Should().ContainKey(allHeaders[0].HashBlock);
            currentHeaders.Should().ContainKey(allHeaders[1].HashBlock);
            currentHeaders.Should().ContainKey(allHeaders[2].HashBlock);
            currentHeaders[allHeaders[2].HashBlock].Next.Should().BeEmpty();
        }

        /// <summary>
        /// Issue 37 @ CT advances at 1000 blocks. Make sure that block data for headers 1-900 are removed.
        /// </summary>
        [Fact]
        public void ChainTipAdvancesAt1000Blocks_DataForHeadersForFirst900HeadersAreRemoved()
        {
            // Chain header tree setup. Initial chain has 1 header.
            // Example: h1.
            const int initialChainSize = 1;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend a chain by 1000 headers.
            // Example: h1=h2=...=h1001.
            const int extensionSize = 1000;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(chainTip, extensionSize);

            // Peer 1 presents a chain.
            const int peerId = 1;
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(peerId, listOfCurrentChainHeaders);
            ChainedHeader[] consumedHeaders = connectionResult.Consumed.ToArray(extensionSize);
            ChainedHeader[] originalHeaders = chainTip.ToArray(extensionSize);

            // Sync all blocks.
            for (int i = 0; i < extensionSize; i++)
            {
                ChainedHeader currentChainTip = consumedHeaders[i];

                cht.BlockDataDownloaded(currentChainTip, originalHeaders[i].Block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
                cht.ConsensusTipChanged(currentChainTip);
            }

            // Headers 2-901 should have block data null.
            // Headers 901 - 1001 should have block data.
            Dictionary<uint256, ChainedHeader> connectedHeaders = cht.GetChainedHeadersByHash();
            foreach (ChainedHeader consumedHeader in consumedHeaders)
            {
                if (consumedHeader.Height <= (extensionSize - ChainedHeaderTree.KeepBlockDataForLastBlocks))
                {
                    connectedHeaders[consumedHeader.HashBlock].Block.Should().BeNull();
                }
                else
                {
                    connectedHeaders[consumedHeader.HashBlock].Block.Should().NotBeNull();
                }
            }
        }

        /// <summary>
        /// Issue 38 @ CT advances at 150. Alternative chain with fork at 120 and total length 160 is presented.
        /// CT switches to 160. Make sure block data for 0-60 is removed.
        /// </summary>
        [Fact]
        public void ChainTipAdvancesAt150Blocks_AlternativeChainPresented_RelevantBlockDataIsRemoved()
        {
            // Chain header tree setup. Initial chain has 1 header.
            // Example: h1.
            const int initialChainSize = 1;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Create a chain A with 150 headers.
            // Example: h1=h2=...h120=h121=a122=a123=a124=...=a151.
            const int extensionBeforeFork = 120;
            const int chainAExtensionSize = 150;
            chainTip = ctx.ExtendAChain(extensionBeforeFork, chainTip);
            ChainedHeader chainATip = ctx.ExtendAChain(chainAExtensionSize - extensionBeforeFork, chainTip);
            List<BlockHeader> listOfCurrentChainAHeaders = ctx.ChainedHeaderToList(chainATip, chainAExtensionSize);

            // Peer 1 presents a chain A.
            const int peer1Id = 1;
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(peer1Id, listOfCurrentChainAHeaders);
            ChainedHeader[] consumedChainAHeaders = connectionResult.Consumed.ToArray(chainAExtensionSize);
            ChainedHeader[] originalChainAHeaders = chainATip.ToArray(chainAExtensionSize);

            // Sync all blocks for chain A.
            for (int i = 0; i < chainAExtensionSize; i++)
            {
                ChainedHeader currentChainTip = consumedChainAHeaders[i];

                cht.BlockDataDownloaded(currentChainTip, originalChainAHeaders[i].Block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
                cht.ConsensusTipChanged(currentChainTip);
            }

            // Create a chain B with 160 headers and a fork at 121.
            // Example: h1=h2=...h120=h121=b122=b123=b124=...=b161.
            const int chainBExtensionSize = 160;
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBExtensionSize - extensionBeforeFork, chainTip);
            List<BlockHeader> listOfCurrentChainBHeaders = ctx.ChainedHeaderToList(chainBTip, chainBExtensionSize - extensionBeforeFork);

            // Peer 2 presents a chain B.
            const int peer2Id = 2;
            connectionResult = cht.ConnectNewHeaders(peer2Id, listOfCurrentChainBHeaders);
            ChainedHeader[] consumedChainBHeaders = connectionResult.Consumed.ToArray(chainBExtensionSize - extensionBeforeFork);
            ChainedHeader[] originalChainBHeaders = chainBTip.ToArray(chainBExtensionSize - extensionBeforeFork);

            // Sync all new blocks for chain B.
            for (int i = 0; i < chainBExtensionSize - extensionBeforeFork; i++)
            {
                ChainedHeader currentChainTip = consumedChainBHeaders[i];

                cht.BlockDataDownloaded(currentChainTip, originalChainBHeaders[i].Block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
            }

            cht.ConsensusTipChanged(connectionResult.Consumed);

            // Headers 2-60 should have block data null.
            // Headers 61 - 161 should have block data.
            Dictionary<uint256, ChainedHeader> connectedHeaders = cht.GetChainedHeadersByHash();
            ChainedHeader[] allChainBHeaders = connectionResult.Consumed.ToArray(chainBExtensionSize);
            foreach (ChainedHeader consumedHeader in allChainBHeaders)
            {
                if (consumedHeader.Height <= (chainBExtensionSize - ChainedHeaderTree.KeepBlockDataForLastBlocks))
                {
                    connectedHeaders[consumedHeader.HashBlock].Block.Should().BeNull();
                }
                else
                {
                    connectedHeaders[consumedHeader.HashBlock].Block.Should().NotBeNull();
                }
            }
        }

        /// <summary>
        /// Issue 39 @ Initial chain is 20 headers long. Single checkpoint is at 1000. Max reorg is 10. Finalized height is 10.
        /// We receive a chain of 100 headers from peer1. Nothing is marked for download. Peer2 presents a chain which forks
        /// at 50 and goes to 150. Nothing is marked for download but the chain is accepted.
        /// </summary>
        [Fact]
        public void ChainTHasCheckpointAt1000_MaxReorgIs10_TwoChainsPriorTo1000Presented_NothingIsMarkedForDownload()
        {
            // Chain header tree setup. Initial chain has 1 header and it uses checkpoints.
            // Example: h1=h2=...=h20.
            const int initialChainSize = 20;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Set checkpoint at height 1000.
            const int checkpointHeight = 1000;
            ChainedHeader fakeChainTip = ctx.ExtendAChain(checkpointHeight - initialChainSize, chainTip);
            var checkpoint = new CheckpointFixture(checkpointHeight, fakeChainTip.Header);
            ctx.SetupCheckpoints(checkpoint);

            // Setup max reorg of 10.
            const int maxReorg = 10;
            ctx.ChainState.Setup(x => x.MaxReorgLength).Returns(maxReorg);

            // Setup finalized block height to 10.
            ctx.FinalizedBlockMock.Setup(m => m.GetFinalizedBlockInfo()).Returns(new HashHeightPair(uint256.One, 10));

            // Extend a chain by 50 headers.
            // Example: h1=h2=...=h50.
            const int extensionSize = 30;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);

            // Setup chain A that has 100 headers and is based on the previous 30 header extension.
            // Example: h1=h2=..=h50=a51=a52=..=a120.
            const int chainAExtensionSize = 70;
            ChainedHeader chainATip = ctx.ExtendAChain(chainAExtensionSize, chainTip);
            List<BlockHeader> listOfChainAHeaders =
                ctx.ChainedHeaderToList(chainATip, extensionSize + chainAExtensionSize);

            // Peer 1 presents a chain A.
            // Chain accepted but nothing marked for download.
            const int peer1Id = 1;
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(peer1Id, listOfChainAHeaders);
            connectionResult.DownloadFrom.Should().BeNull();
            connectionResult.DownloadTo.Should().BeNull();
            connectionResult.Consumed.HashBlock.Should().Be(chainATip.HashBlock);

            ChainedHeader[] consumedHeaders = connectionResult.Consumed.ToArray(listOfChainAHeaders.Count);
            consumedHeaders.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.HeaderOnly).Should().BeTrue();

            // Setup chain B that extends to height 150 and is based on the previous 30 header extension, i.e. fork point at 50.
            // Example: h1=h2=..=h50=b51=b52=..=b150.
            const int chainBExtensionSize = 100;
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBExtensionSize, chainTip);
            List<BlockHeader> listOfChainBHeaders =
                ctx.ChainedHeaderToList(chainBTip, extensionSize + chainBExtensionSize);

            // Peer 2 presents a chain B.
            // Chain accepted but nothing marked for download.
            const int peer2Id = 2;
            connectionResult = cht.ConnectNewHeaders(peer2Id, listOfChainBHeaders);
            connectionResult.DownloadFrom.Should().BeNull();
            connectionResult.DownloadTo.Should().BeNull();
            connectionResult.Consumed.HashBlock.Should().Be(chainBTip.HashBlock);
            consumedHeaders = connectionResult.Consumed.ToArray(listOfChainAHeaders.Count);
            consumedHeaders.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.HeaderOnly).Should().BeTrue();
        }

        /// <summary>
        /// Issue 18 @ Peer A starts to claim chain that D claims. Make sure that 8a, 9a are disconnected.
        /// </summary>
        [Fact]
        public void PeerAClaimsChainThatPeerDClaims_Heads8Aand9A_Shoulddisconect()
        {
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;
            ChainedHeaderBlock consensusTip = cht.GetChainedHeaderBlock(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId]);

            ctx.SetupPeersForTest(cht, initialChainTip);

            // Additional SetUp for current test.
            ChainedHeader chainATip = cht.GetPeerTipChainedHeaderByPeerId(0);
            ChainedHeader[] last2HeadersPeerA = {
                chainATip.Previous, chainATip
            };
            var intitialChainedHeaders = new Dictionary<uint256, ChainedHeader>(cht.GetChainedHeadersByHash());

            // Checking that 8a, 9a are in chained tree.
            intitialChainedHeaders.Should().ContainValues(last2HeadersPeerA);

            ChainedHeader chainDTip = cht.GetPeerTipChainedHeaderByPeerId(3);

            // Peed A claims Peer D chain.
            chainATip = chainDTip;

            List<BlockHeader> peerABlockHeaders = ctx.ChainedHeaderToList(chainATip, chainATip.Height);

            cht.ConnectNewHeaders(0, peerABlockHeaders);

            var chainedHeadersAfterPeerAChanged = new Dictionary<uint256, ChainedHeader>(cht.GetChainedHeadersByHash());

            // Checking that 8a, 9a are not presented in chained tree anymore.
            chainedHeadersAfterPeerAChanged.Should().NotContainValues(last2HeadersPeerA);

            this.CheckChainedHeaderTreeConsistency(cht, ctx, consensusTip, new HashSet<int>() { 0, 1, 2, 3 });
        }

        /// <summary>
        /// Issue 19 @ New peer K claims 10d. Peer K disconnects. Chain shouldn't change.
        /// </summary>
        [Fact]
        public void NewPeerClaimsHeadSecondTime_PeerDisconnected_ChainShouldNotChange()
        {
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;
            ChainedHeaderBlock consensusTip = cht.GetChainedHeaderBlock(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId]);

            ctx.SetupPeersForTest(cht, initialChainTip);

            // Additional SetUp for current test.
            ChainedHeader chainDTip = cht.GetPeerTipChainedHeaderByPeerId(3);
            ChainedHeader chainKTip = chainDTip; // peer K has exactly the same chain as peer D.
            List<BlockHeader> peerKBlockHeaders = ctx.ChainedHeaderToList(chainKTip, chainKTip.Height);

            cht.ConnectNewHeaders(4, peerKBlockHeaders);

            var chainedHeadersWithPeerK = new Dictionary<uint256, ChainedHeader>(cht.GetChainedHeadersByHash());

            cht.PeerDisconnected(4);

            Dictionary<uint256, ChainedHeader> chainedHeadersWithoutPeerK = cht.GetChainedHeadersByHash();

            chainedHeadersWithoutPeerK.Should().BeEquivalentTo(chainedHeadersWithPeerK);

            this.CheckChainedHeaderTreeConsistency(cht, ctx, consensusTip, new HashSet<int>() { 0, 1, 2, 3 });
        }

        /// <summary>
        /// Issue 20 @ New peer K is connected, it prolongs it prolongs D’s chain by 2 headers. K is disconnected, only those 2 headers are removed.
        /// </summary>
        [Fact]
        public void NewPeerProlongsByTwoHeaders_PeerDisconnected_NewTwoHeadersRemoved()
        {
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;
            ChainedHeaderBlock consensusTip = cht.GetChainedHeaderBlock(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId]);

            ctx.SetupPeersForTest(cht, initialChainTip);

            // Additional SetUp for current test.
            int peerKExtension = 2;
            ChainedHeader chainDTip = cht.GetPeerTipChainedHeaderByPeerId(3);
            ChainedHeader chainKTip = ctx.ExtendAChain(peerKExtension, chainDTip); // peer K prolongs peer D by 2 headers.

            var chainedHeadersBeforePeerKConnected = new Dictionary<uint256, ChainedHeader>(cht.GetChainedHeadersByHash());

            List<BlockHeader> peerKBlockHeaders = ctx.ChainedHeaderToList(chainKTip, chainKTip.Height);
            cht.ConnectNewHeaders(4, peerKBlockHeaders);

            var chainedHeadersWithPeerK = new Dictionary<uint256, ChainedHeader>(cht.GetChainedHeadersByHash());

            // Double checking that chained tree has been changed after connecting new peer.
            chainedHeadersBeforePeerKConnected.Should().NotEqual(chainedHeadersWithPeerK);

            cht.PeerDisconnected(4);

            var chainedHeadersAfterPeerKDisconnected = new Dictionary<uint256, ChainedHeader>(cht.GetChainedHeadersByHash());

            chainedHeadersBeforePeerKConnected.Should().BeEquivalentTo(chainedHeadersAfterPeerKDisconnected);

            this.CheckChainedHeaderTreeConsistency(cht, ctx, consensusTip, new HashSet<int>() { 0, 1, 2, 3 });
        }

        /// <summary>
        /// Basic test for tests 18 to 20, 28.
        /// 1/6 There is no branch which is claimed by no one (PeerTipsByHash have tip of every branch), none of list should be empty.
        /// 2/6 ChainHeadersByHash contains only reachable headers.
        /// 3/6 ConsensusTip is claimed by LocalPeer.
        /// 4/6 Each connected peer has exactly 1 entry in PeerIdsByTipHash.
        /// 5/6 CH.Next[i].Prev == CH for every header and every .Next.
        /// 6/6 PeerTipsByPeerId should reflect PeerIdsByHash(except local marker).
        /// </summary>
        /// <param name="cht">ChainHeaderTree.</param>
        /// <param name="ctx">Test context</param>
        /// <param name="consensusTip">Consensus Tip</param>
        /// <param name="connectedPeers">Peer ids of the peers that are expected to be connected.</param>
        private void CheckChainedHeaderTreeConsistency(ChainedHeaderTree cht, TestContext ctx, ChainedHeaderBlock consensusTip, HashSet<int> connectedPeers)
        {
            bool eachPeerOneEntry = true;

            var peerEntryDictionary = new Dictionary<int, int>();
            var peerHeaderDictionary = new Dictionary<int, int>();

            Dictionary<uint256, HashSet<int>> tipsDictionary = cht.GetPeerIdsByTipHash();
            Dictionary<int, uint256> peerIdsDictionary = cht.GetPeerTipsByPeerId();

            foreach (int key in peerIdsDictionary.Keys)
            {
                peerHeaderDictionary.Add(key, 0);
                peerEntryDictionary.Add(key, 0);
            }

            var tipsLeft = new HashSet<uint256>(cht.GetChainedHeadersByHash().Select(x => x.Value).Where(x => x.Next.Count == 0).Select(x => x.HashBlock));

            foreach (KeyValuePair<uint256, HashSet<int>> tips in tipsDictionary)
            {
                // There should be no branch which is claimed by no one.
                Assert.NotEmpty(tips.Value);

                tipsLeft.Remove(tips.Key);

                foreach (int peerId in tips.Value)
                {
                    // Ignore local.
                    if (peerId != ChainedHeaderTree.LocalPeerId)
                    {
                        Assert.Contains(peerId, connectedPeers);

                        peerEntryDictionary[peerId] = peerEntryDictionary[peerId]++;

                        if (peerIdsDictionary[peerId] == tips.Key)
                            peerHeaderDictionary[peerId]++;
                        else
                            Assert.True(false, "PeerTipsByPeerId should reflect PeerIdsByHash(except local marker).");
                    }
                }
            }

            Assert.Empty(tipsLeft);

            // Checking "Each connected peer has exactly 1 entry in PeertipsByHash".
            if (peerEntryDictionary.Count(x => x.Value > 1) > 0) eachPeerOneEntry = false;
            if (peerHeaderDictionary.Count(x => x.Value > 1) > 0) eachPeerOneEntry = false;
            Assert.True(eachPeerOneEntry);

            Dictionary<uint256, ChainedHeader> chainHeaders = cht.GetChainedHeadersByHash();

            foreach (ChainedHeader header in chainHeaders.Values)
            {
                for (int i = 0; i < header.Next.Count; i++)
                {
                    // Is correct chain sequence.
                    Assert.Equal(header.Next[i].Previous.HashBlock, header.HashBlock);
                }
            }

            // ConsensusTip is claimed by LocalPeer.
            Assert.Equal(consensusTip.ChainedHeader.HashBlock, peerIdsDictionary[ChainedHeaderTree.LocalPeerId]);

            // ChainedHeadersByHash contains only reachable headers.
            var allConnectedHeaders = new List<ChainedHeader>();

            ChainedHeader genesis = cht.GetChainedHeadersByHash()[ctx.Network.GenesisHash];
            var headersToProcess = new Stack<ChainedHeader>();
            headersToProcess.Push(genesis);

            while (headersToProcess.Count > 0)
            {
                ChainedHeader current = headersToProcess.Pop();
                Assert.True(chainHeaders.ContainsKey(current.HashBlock));
                allConnectedHeaders.Add(current);
                foreach (ChainedHeader next in current.Next)
                    headersToProcess.Push(next);
            }

            Assert.Equal(chainHeaders.Count, allConnectedHeaders.Count);

            // Make sure there are no tips of peers that are not connected.
            Assert.Equal(cht.GetPeerTipsByPeerId().Count, connectedPeers.Count + 1);

            foreach (int peerId in cht.GetPeerTipsByPeerId().Keys)
            {
                if (peerId == ChainedHeaderTree.LocalPeerId)
                    continue;

                Assert.Contains(peerId, connectedPeers);
            }

            Assert.Contains(ChainedHeaderTree.LocalPeerId, cht.GetPeerTipsByPeerId().Keys);
        }

        /// <summary>
        /// Issue 28 @ Peers E,F claims 10d. PartialOrFullValidationFailed(7a), make sure that 7a,8a,9a,8d,9d,10d,11d are removed and the peers A, E, F, D are marked as PeersToBan.
        /// </summary>
        [Fact]
        public void PeerEAndFClaimsHead_PartialOrFullValidationFailed_RestOfHeadMustBeRemoved_PeersMarkedAsPeersToBan()
        {
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;
            ChainedHeaderBlock consensusTip = cht.GetChainedHeaderBlock(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId]);

            ctx.SetupPeersForTest(cht, initialChainTip);

            // Additional SetUp for current test.
            ChainedHeader chainATip = cht.GetPeerTipChainedHeaderByPeerId(0);
            ChainedHeader chainDTip = cht.GetPeerTipChainedHeaderByPeerId(3);
            ChainedHeader chainETip = ctx.ExtendAChain(2, chainDTip.GetAncestor(10)); // i.e. ((h1=h2=h3=h4=h5)=6a=7a)=8d=9d=10d)=11e
            ChainedHeader chainFTip = ctx.ExtendAChain(2, chainDTip.GetAncestor(10)); // i.e. ((h1=h2=h3=h4=h5)=6a=7a)=8d=9d=10d)=11f
            List<BlockHeader> peerEBlockHeaders = ctx.ChainedHeaderToList(chainETip, chainETip.Height);
            List<BlockHeader> peerFBlockHeaders = ctx.ChainedHeaderToList(chainFTip, chainFTip.Height);

            ConnectNewHeadersResult eResult = cht.ConnectNewHeaders(5, peerEBlockHeaders);
            cht.ConnectNewHeaders(6, peerFBlockHeaders);

            var peerIdsByHash = new Dictionary<int, uint256>(cht.GetPeerTipsByPeerId());

            var dictionaryAffectedPeers = new Dictionary<int, uint256>()
            {
                {0, peerIdsByHash[0]},
                {3, peerIdsByHash[3]},
                {5, peerIdsByHash[5]},
                {6, peerIdsByHash[6]}
            };

            List<ChainedHeader> listOfChainedHeaders = chainATip.ToArray(chainATip.Height).Where(x => x.Height >= 7).ToList();
            listOfChainedHeaders.AddRange(chainDTip.ToArray(chainDTip.Height).Where(x => x.Height >= 7).ToList());
            listOfChainedHeaders.AddRange(chainETip.ToArray(chainETip.Height).Where(x => x.Height >= 7).ToList());
            listOfChainedHeaders.AddRange(chainFTip.ToArray(chainFTip.Height).Where(x => x.Height >= 7).ToList());
            List<ChainedHeader> listOfChainedHeadersMustBeRemovedFromCht = listOfChainedHeaders.Distinct().ToList();

            ChainedHeader[] consumedHeaders = eResult.Consumed.ToArray(12);
            List<int> peersToBan = cht.PartialOrFullValidationFailed(consumedHeaders.FirstOrDefault(x => x.Height == 7)); // 7a validation failed.
            peersToBan.Count.Should().Be(4); // Check that just four peers have been banned.

            List<uint256> peerIdsByHashAfterFail = cht.GetPeerIdsByTipHash().Select(x => x.Key).ToList();
            List<ChainedHeader> chainedHeadersAfterFail = cht.GetChainedHeadersByHash().Select(x => x.Value).ToList();

            chainedHeadersAfterFail.Should().NotContain(listOfChainedHeadersMustBeRemovedFromCht); // Check that headers have been removed.

            peersToBan.Should().Contain(dictionaryAffectedPeers.Select(x => x.Key).ToList()); // Check that Peers A, D, E, F are in ban list.

            peerIdsByHashAfterFail.Should().NotContain(dictionaryAffectedPeers.Select(x => x.Value).ToList()); // Check that Peers A, D, E, F have been disconected.

            this.CheckChainedHeaderTreeConsistency(cht, ctx, consensusTip, new HashSet<int>() { 1, 2 });
        }

        /// <summary>
        /// Issue 40 @ CT advances after last checkpoint to height LC + MaxReorg + 10. New chain is presented with
        /// fork point at LC + 5, chain is not accepted.
        /// </summary>
        [Fact]
        public void ChainAdvancesAfterACheckpoint_NewChainIsPresentedWithForkPoint_ChainIsNotAccepted()
        {
            // Chain header tree setup. Initial chain has 5 headers and checkpoint at h5.
            // Example: h1=h2=h3=h4=(h5).
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            const int checkpointHeight = 5;
            var checkpoint = new CheckpointFixture(checkpointHeight, chainTip.Header);
            ctx.SetupCheckpoints(checkpoint);

            // Setup max reorg to 100.
            const int maxReorg = 100;
            ctx.ChainState.Setup(x => x.MaxReorgLength).Returns(maxReorg);

            // Extend the chain with (checkpoint + MaxReorg + 10) headers, i.e. 115 headers.
            const int extensionSize = 10;
            const int chainASize = checkpointHeight + maxReorg + extensionSize;
            ChainedHeader chainATip = ctx.ExtendAChain(chainASize, chainTip);

            // Chain A is presented by peer 1.
            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(chainATip, chainASize);
            ChainedHeader consumed = cht.ConnectNewHeaders(1, listOfChainABlockHeaders).Consumed;
            ChainedHeader[] consumedChainAHeaders = consumed.ToArray(chainASize);
            ChainedHeader[] originalChainAHeaders = chainATip.ToArray(chainASize);

            // Sync all blocks from chain A.
            for (int i = 0; i < chainASize; i++)
            {
                ChainedHeader currentChainTip = consumedChainAHeaders[i];
                Block block = originalChainAHeaders[i].Block;

                cht.BlockDataDownloaded(currentChainTip, block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
                ctx.FinalizedBlockMock.Setup(m => m.GetFinalizedBlockInfo()).Returns(new HashHeightPair(uint256.One, currentChainTip.Height - maxReorg));
                cht.ConsensusTipChanged(currentChainTip);
            }

            // Create new chain B with 20 headers and a fork point at height 10.
            const int forkPointHeight = 10;
            const int chainBSize = 20;
            ChainedHeader forkTip = chainATip.GetAncestor(forkPointHeight);
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBSize - forkPointHeight, forkTip);

            // Chain B is presented by peer 2.
            List<BlockHeader> listOfChainBHeaders = ctx.ChainedHeaderToList(chainBTip, chainBSize);
            Action connectHeadersAction = () => cht.ConnectNewHeaders(2, listOfChainBHeaders);

            // Connection fails.
            connectHeadersAction.Should().Throw<MaxReorgViolationException>();
        }

        /// <summary>
        /// Issue 41 @ BlockDataDownloaded called on 10 known blocks.
        /// Make sure that UnconsumedBlocksDataBytes is equal to the sum of serialized sizes of those blocks.
        /// </summary>
        [Fact]
        public void BlockDataDownloadedIsCalled_UnconsumedBlocksDataBytes_Equals_SumOfSerializedBlockSize()
        {
            const int initialChainSize = 5;
            const int chainExtension = 10;

            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            ChainedHeader extendedChainTip = testContext.ExtendAChain(chainExtension, initialChainTip);
            List<BlockHeader> listOfChainBlockHeaders = testContext.ChainedHeaderToList(extendedChainTip, initialChainSize + chainExtension);

            // Present all chain headers h1->h15.
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfChainBlockHeaders);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            int serializedSizeOfBlocks = 0;
            foreach (ChainedHeader chainedHeader in connectNewHeadersResult.ToArray())
            {
                chainedHeaderTree.BlockDataDownloaded(chainedHeader, extendedChainTip.FindAncestorOrSelf(chainedHeader).Block);
                serializedSizeOfBlocks += chainedHeader.Block.GetSerializedSize();
            }

            // UnconsumedBlocksDataBytes is equal to the sum of serialized sizes of the blocks.
            Assert.Equal(chainedHeaderTree.UnconsumedBlocksDataBytes, serializedSizeOfBlocks);
        }

        /// <summary>
        /// Issue 42 @ CT is at 0. 10 headers are presented.
        /// 10 blocks are downloaded. CT advances to 5.
        /// Make sure that UnconsumedBlocksDataBytes is equal to
        /// the sum of serialized sizes of last five blocks.
        /// CT advances to 10.  Make sure UnconsumedBlocksDataBytes is 0.
        /// </summary>
        [Fact]
        public void PresentHeaders_ChainHeaderTreeAdvances_UnconsumedBlocksDataBytes_Equals_SumOfSerializedBlockSize()
        {
            const int initialChainSize = 0;
            const int chainExtensionSize = 10;

            // Chain header tree setup.
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;

            // 10 headers are presented.
            ChainedHeader commonChainTip = ctx.ExtendAChain(chainExtensionSize, initialChainTip);
            List<BlockHeader> listOfChainBlockHeaders = ctx.ChainedHeaderToList(commonChainTip, chainExtensionSize);

            // 10 blocks are downloaded.
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, listOfChainBlockHeaders);
            ChainedHeader chainedHeaderTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderTo.HashBlock.Should().Be(commonChainTip.HashBlock);

            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            foreach (ChainedHeader chainedHeader in connectNewHeadersResult.ToArray())
            {
                chainedHeaderTree.BlockDataDownloaded(chainedHeader, commonChainTip.FindAncestorOrSelf(chainedHeader).Block);
                chainedHeaderTree.PartialValidationSucceeded(chainedHeader, out bool fullValidationRequired);
                fullValidationRequired.Should().BeTrue();
            }

            // CT advances to 5.
            chainedHeaderTree.ConsensusTipChanged(chainedHeaderTo.GetAncestor(5));

            int serializedSizeOfBlocks = 0;
            IEnumerable<ChainedHeader> lastFive = connectNewHeadersResult.ToArray().TakeLast(5);
            foreach (ChainedHeader chainedHeader in lastFive)
            {
                serializedSizeOfBlocks += chainedHeader.Block.GetSerializedSize();
            }

            // UnconsumedBlocksDataBytes is non-zero and equal to the sum of serialized sizes of last five blocks.
            Assert.NotEqual(0, chainedHeaderTree.UnconsumedBlocksDataBytes);
            Assert.Equal(serializedSizeOfBlocks, chainedHeaderTree.UnconsumedBlocksDataBytes);

            // CT advances to 10.
            chainedHeaderTree.ConsensusTipChanged(chainedHeaderTo);

            // UnconsumedBlocksDataBytes is 0.
            Assert.Equal(0, chainedHeaderTree.UnconsumedBlocksDataBytes);
        }

        /// <summary>
        /// Issue 43 @ CT is at 0. 10 headers are presented. 10 blocks are downloaded.
        /// CT advances to 5. Alternative chain with fork at 3 and tip at 12 is presented.
        /// Block data for alternative chain is downloaded. CT changes to block 8 of the 2nd chain.
        /// Make sure that UnconsumedBlocksDataBytes is equal to the sum of
        /// serialized sizes of 9b-12b + 6a-10a (a- first chain, b- second chain).
        /// </summary>
        [Fact]
        public void PresentHeaders_BlocksDownloaded_ForkPresented_BlockDataForAlternativeChainDownloaded_ChainTipChanges()
        {
            const int initialChainSize = 0;
            const int chainExtensionSize = 10;
            const int peerOneId = 1;
            const int peerTwoId = 2;

            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // Chain tip is at 0.
            Assert.Equal(0, initialChainTip.Height);

            // Headers are presented for h1 -> h10.
            ChainedHeader extendedChainTip = testContext.ExtendAChain(chainExtensionSize, initialChainTip);
            List<BlockHeader> listOfExtendedChainBlockHeaders = testContext.ChainedHeaderToList(extendedChainTip, chainExtensionSize);
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerOneId, listOfExtendedChainBlockHeaders);
            Assert.Equal(connectNewHeadersResult.DownloadFrom.Header, extendedChainTip.GetAncestor(initialChainSize + 1).Header); // h1
            Assert.Equal(connectNewHeadersResult.DownloadTo.Header, extendedChainTip.Header); // h10
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // 10 blocks are downloaded.
            foreach (ChainedHeader chainedHeader in connectNewHeadersResult.ToArray())
            {
                chainedHeaderTree.BlockDataDownloaded(chainedHeader, extendedChainTip.FindAncestorOrSelf(chainedHeader).Block);
                chainedHeaderTree.PartialValidationSucceeded(chainedHeader, out bool fullValidationRequired);

                if (chainedHeader.Height <= 5) // CT advances to 5.
                {
                    chainedHeaderTree.ConsensusTipChanged(chainedHeader);
                }
            }

            // Alternative chain with fork at h3 and tip at h12 is presented.
            const int heightOfFork = 3;
            const int chainBExtension = 9;
            ChainedHeader forkedChainHeader = connectNewHeadersResult.DownloadTo.GetAncestor(heightOfFork);
            ChainedHeader tipOfFork = testContext.ExtendAChain(chainBExtension, forkedChainHeader);
            Assert.Equal(12, tipOfFork.Height);

            // Headers are presented for h3 -> h12.
            listOfExtendedChainBlockHeaders = testContext.ChainedHeaderToList(tipOfFork, tipOfFork.Height - heightOfFork);
            connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerTwoId, listOfExtendedChainBlockHeaders);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));
            Assert.Equal(connectNewHeadersResult.DownloadFrom.Header, tipOfFork.GetAncestor(heightOfFork + 1).Header);
            Assert.Equal(connectNewHeadersResult.DownloadTo.Header, tipOfFork.Header);

            foreach (ChainedHeader chainedHeader in connectNewHeadersResult.ToArray())
            {
                chainedHeaderTree.BlockDataDownloaded(chainedHeader, tipOfFork.FindAncestorOrSelf(chainedHeader).Block);
                chainedHeaderTree.PartialValidationSucceeded(chainedHeader, out bool fullValidationRequired);

                if (chainedHeader.Height <= 8)  // CT advances to 8.
                {
                    chainedHeaderTree.ConsensusTipChanged(chainedHeader);
                }
            }

            // UnconsumedBlocksDataBytes is equal to the sum of serialized sizes of 9b-12b + 6a-10a.
            int serializedSizeOfChainA = 0;
            int serializedSizeOfChainB = 0;

            ChainedHeader chainedHeaderChainA = extendedChainTip;
            while (chainedHeaderChainA.Height >= 6)
            {
                serializedSizeOfChainA += chainedHeaderChainA.Block.GetSerializedSize();
                chainedHeaderChainA = chainedHeaderChainA.Previous;
            }

            ChainedHeader chainedHeaderChainB = tipOfFork;
            while (chainedHeaderChainB.Height >= 9)
            {
                serializedSizeOfChainB += chainedHeaderChainB.Block.GetSerializedSize();
                chainedHeaderChainB = chainedHeaderChainB.Previous;
            }

            int serializedSizeOfChainsAandB = serializedSizeOfChainA + serializedSizeOfChainB;
            Assert.Equal(chainedHeaderTree.UnconsumedBlocksDataBytes, serializedSizeOfChainsAandB);
        }

        /// <summary>
        /// Issue 44 @ CT is at 0. 10 headers are presented. 10 blocks are downloaded.
        /// CT advances to 5. Make sure that UnconsumedBlocksDataBytes is
        /// equal to the sum of serialized sizes of the last five blocks.
        /// Second peer claims the same chain but till block 8.
        /// First peer that presented this chain disconnected.
        /// Make sure that UnconsumedBlocksDataBytes is equal to sum of block sizes of 6,7,8.
        /// </summary>
        [Fact]
        public void PresentHeaders_BlocksDownloaded_UnconsumedBlocksDataBytes_Equals_SerializedSizesOfLastBlocks()
        {
            const int peer1Id = 1;
            const int peer2Id = 2;
            const int initialChainSize = 0;
            const int chainExtensionSize = 10;

            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // 10 headers are presented.
            ChainedHeader chainTip = testContext.ExtendAChain(chainExtensionSize, initialChainTip);
            List<BlockHeader> listOfExtendedChainBlockHeaders =
                testContext.ChainedHeaderToList(chainTip, chainExtensionSize);

            // 10 blocks are downloaded.
            ConnectNewHeadersResult connectNewHeadersResult =
                chainedHeaderTree.ConnectNewHeaders(peer1Id, listOfExtendedChainBlockHeaders);
            Assert.Equal(connectNewHeadersResult.DownloadFrom.Header, listOfExtendedChainBlockHeaders.First());
            Assert.Equal(connectNewHeadersResult.DownloadTo.Header, listOfExtendedChainBlockHeaders.Last());
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            foreach (ChainedHeader chainedHeader in connectNewHeadersResult.ToArray())
            {
                chainedHeaderTree.BlockDataDownloaded(chainedHeader, chainTip.FindAncestorOrSelf(chainedHeader).Block);
                if (chainedHeader.Height <= 5)
                {
                    chainedHeaderTree.PartialValidationSucceeded(chainedHeader, out bool fullValidationRequired);
                    fullValidationRequired.Should().BeTrue();
                    chainedHeaderTree.ConsensusTipChanged(chainedHeader);
                }
            }

            // Make sure that UnconsumedBlocksDataBytes is equal to the sum of serialized sizes of the last five blocks.
            int serializedSizeOfChain = 0;

            ChainedHeader chainedHeaderChain = chainTip;
            while (chainedHeaderChain.Height > 5)
            {
                serializedSizeOfChain += chainedHeaderChain.Block.GetSerializedSize();
                chainedHeaderChain = chainedHeaderChain.Previous;
            }

            Assert.Equal(chainedHeaderTree.UnconsumedBlocksDataBytes, serializedSizeOfChain);

            // Second peer claims the same chain but till block 8.
            const int headersBeyondBlockEight = 2;
            chainedHeaderTree.ConnectNewHeaders(peer2Id, listOfExtendedChainBlockHeaders.SkipLast(headersBeyondBlockEight).ToList());
            ChainedHeader secondPeerTip = chainedHeaderTree.GetChainedHeadersByHash()[chainedHeaderTree.GetPeerTipsByPeerId()[peer2Id]];
            Assert.Equal(8, secondPeerTip.Height);

            // First peer that presented this chain disconnected.
            chainedHeaderTree.PeerDisconnected(peer1Id);

            // Make sure that UnconsumedBlocksDataBytes is equal to sum of block sizes of 6,7,8.
            serializedSizeOfChain = 0;
            chainedHeaderChain = secondPeerTip;

            while (chainedHeaderChain.Height >= 6)
            {
                serializedSizeOfChain += chainedHeaderChain.Block.GetSerializedSize();
                chainedHeaderChain = chainedHeaderChain.Previous;
            }
            Assert.Equal(chainedHeaderTree.UnconsumedBlocksDataBytes, serializedSizeOfChain);
        }

        /// <summary>
        /// Issue 46 @ CT is at 5. Checkpoint is at 10.
        /// ConnectNewHeaders called with 9 new headers (from peer1).
        /// After that ConnectNewHeaders called with headers 5 to 15 (from peer2).
        /// Make sure 6 to 15 are marked for download.
        /// </summary>
        [Fact]
        public void PresentHeaders_PresentHeadersFromAlternatePeer_MarkedForDownload()
        {
            const int initialChainSizeOfFive = 5;
            const int chainExtensionSizeOfFive = 5;
            const int peerOneId = 1;
            const int peerTwoId = 2;

            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSizeOfFive).UseCheckpoints(true).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // Chain tip is at h5.
            Assert.Equal(initialChainSizeOfFive, initialChainTip.Height);

            // 5 more headers are presented.
            ChainedHeader extendedChainTip = testContext.ExtendAChain(chainExtensionSizeOfFive, initialChainTip);
            List<BlockHeader> listOfChainBlockHeaders = testContext.ChainedHeaderToList(extendedChainTip, initialChainSizeOfFive + chainExtensionSizeOfFive);

            // Checkpoint is at h10.
            const int checkpointHeight = 10;
            var checkpoint = new CheckpointFixture(checkpointHeight, listOfChainBlockHeaders[checkpointHeight - 1]);
            testContext.SetupCheckpoints(checkpoint);

            // Peer 1 presents nine headers up to checkpoint: h1 -> h9.
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerOneId, listOfChainBlockHeaders.Take(9).ToList());
            connectNewHeadersResult.DownloadFrom.Should().Be(null);
            connectNewHeadersResult.DownloadTo.Should().Be(null);

            // Peer 2 presents ten headers including checkpoint: h5 -> h15.
            extendedChainTip = testContext.ExtendAChain(chainExtensionSizeOfFive, extendedChainTip); // tip h15
            listOfChainBlockHeaders = testContext.ChainedHeaderToList(extendedChainTip, initialChainSizeOfFive + chainExtensionSizeOfFive * 2);
            connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerTwoId, listOfChainBlockHeaders.GetRange(initialChainSizeOfFive - 1, 11).ToList());

            // Headers h6 -> 15 should be marked for download.
            connectNewHeadersResult.DownloadFrom.HashBlock.Should().Be(extendedChainTip.GetAncestor(initialChainSizeOfFive + 1).HashBlock); // h6
            connectNewHeadersResult.DownloadTo.HashBlock.Should().Be(extendedChainTip.HashBlock); // h15
            connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired).Should().BeTrue();
        }

        /// <summary>
        /// Issue 47 @ CT is at 5. Checkpoints are at 10 and 20.
        /// ConnectNewHeaders called with 9 new headers (from peer1).
        /// After that ConnectNewHeaders called with headers 5 to 15 (from peer2).
        /// Make sure 6 - 10 are requested for download.
        /// </summary>
        [Fact]
        public void PresentHeaders_CheckpointsEnabledAndSet_PresentHeadersFromAlternatePeer_MarkedForDownload()
        {
            const int initialChainSizeOfFiveHeaders = 5;
            const int chainExtensionSizeOfFifteenHeaders = 15;
            const int peerOneId = 1;
            const int peerTwoId = 2;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSizeOfFiveHeaders).UseCheckpoints().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // Chain tip is at h5.
            Assert.Equal(initialChainSizeOfFiveHeaders, initialChainTip.Height);

            // Extend chain to h20.
            ChainedHeader extendedChainTip = testContext.ExtendAChain(chainExtensionSizeOfFifteenHeaders, initialChainTip);
            List<BlockHeader> listOfExtendedChainHeaders =
                testContext.ChainedHeaderToList(extendedChainTip, initialChainSizeOfFiveHeaders + chainExtensionSizeOfFifteenHeaders);

            // Checkpoints are at h10 and h20.
            const int checkpoint1Height = 10;
            const int checkpoint2Height = 20;

            var checkpoint1 = new CheckpointFixture(checkpoint1Height, listOfExtendedChainHeaders[checkpoint1Height - 1]);
            var checkpoint2 = new CheckpointFixture(checkpoint2Height, listOfExtendedChainHeaders[checkpoint2Height - 1]);
            testContext.SetupCheckpoints(checkpoint1, checkpoint2);

            // First peer presents headers up to but excluding first checkpoint h1 -> h9.
            List<BlockHeader> listOfBlockHeadersOneToNine = listOfExtendedChainHeaders.Take(9).ToList();
            chainedHeaderTree.ConnectNewHeaders(peerOneId, listOfBlockHeadersOneToNine);

            // Second peer presents headers including checkpoint1, excluding checkpoint2: h5 -> h15.
            List<BlockHeader> listOfBlockHeadersFiveToFifteen = listOfExtendedChainHeaders.GetRange(initialChainSizeOfFiveHeaders - 1, 11);
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerTwoId, listOfBlockHeadersFiveToFifteen);

            // Headers h6 -> h10 should be marked for download.
            connectNewHeadersResult.DownloadFrom.HashBlock.Should().Be(extendedChainTip.GetAncestor(initialChainSizeOfFiveHeaders + 1).HashBlock); // h6
            connectNewHeadersResult.DownloadTo.HashBlock.Should().Be(extendedChainTip.GetAncestor(checkpoint1Height).HashBlock); // h10
            connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired).Should().BeTrue();

            // Headers h11 -> h20 have availability state of header only.
            ChainedHeader chainedHeader = extendedChainTip;
            while (chainedHeader.Height > connectNewHeadersResult.DownloadTo.Height)
            {
                Assert.Equal(BlockDataAvailabilityState.HeaderOnly, chainedHeader.BlockDataAvailability);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// Issue 48 @ CT is at 5. AssumeValid is at 10. ConnectNewHeaders called with headers 1 - 9 (from peer1).
        /// Make sure headers 6 - 9 are marked for download. After that ConnectNewHeaders called with headers 5 to 15 (from peer2).
        /// Make sure 9 - 15 are marked for download.
        /// </summary>
        [Fact]
        public void ConsensusTipAtHeight5_AssumedValidIsAt10_WhenConnectingHeadersByDifferentPeers_CorrectHeadersAreMarkedForDownload()
        {
            // Chain header tree setup.
            // Initial chain has 5 headers.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend a chain tip by 10 headers and set assumed valid to header 10.
            // Example: h1=h2=h3=h4=h5=h6=h7=h8=h9=(h10)=h11=h12=h13=h14=h15.
            const int extensionSize = 10;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);
            List<BlockHeader> listOfChainBlockHeaders = ctx.ChainedHeaderToList(chainTip, initialChainSize + extensionSize);
            ctx.ConsensusSettings.BlockAssumedValid = listOfChainBlockHeaders[9].GetHash();

            ChainedHeader[] originalHeaders = chainTip.ToArray(initialChainSize + extensionSize);

            // Peer 1 presents 9 headers: h1 - h9.
            // Headers 6-9 should be marked for download.
            ConnectNewHeadersResult connectNewHeadersResult = cht.ConnectNewHeaders(1, listOfChainBlockHeaders.Take(9).ToList());
            connectNewHeadersResult.DownloadFrom.HashBlock.Should().Be(originalHeaders[5].HashBlock); // h6
            connectNewHeadersResult.DownloadTo.HashBlock.Should().Be(originalHeaders[8].HashBlock); // h9
            connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired).Should().BeTrue();

            // Peer 2 presents 10 headers: h6 - h15.
            // Headers 10-15 should be marked for download.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfChainBlockHeaders.Skip(5).ToList());
            connectNewHeadersResult.DownloadFrom.HashBlock.Should().Be(originalHeaders[9].HashBlock); // h10
            connectNewHeadersResult.DownloadTo.HashBlock.Should().Be(originalHeaders[14].HashBlock); // h15
            connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired).Should().BeTrue();
        }

        /// <summary>
        /// Issue 50 @ CHT is initialized with BlockStore enabled. CT advances and old block data pointers are removed.
        /// Make sure that data availability for those headers set to data available.
        /// </summary>
        [Fact]
        public void ChainHeaderTreeInitialisedWIthBlockStoreEnabled_ConsensusTipAdvances_AvailabilityForHeadersSetToDataAvailable()
        {
            // Chain header tree setup. Initial chain has 1 header.
            // Example: h1.
            const int initialChainSize = 1;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chain by 150 headers and connect it to CHT.
            // Example: h1=h2=..=h151.
            const int extensionSize = 150;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);
            List<BlockHeader> listOfExtendedHeaders = ctx.ChainedHeaderToList(chainTip, extensionSize);
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(1, listOfExtendedHeaders);
            ChainedHeader consumed = connectionResult.Consumed;

            // Sync all headers.
            ChainedHeader[] originalHeaderArray = chainTip.ToArray(extensionSize);
            ChainedHeader[] consumedHeaderArray = consumed.ToArray(extensionSize);
            for (int i = 0; i < consumedHeaderArray.Length; i++)
            {
                ChainedHeader currentChainTip = consumedHeaderArray[i];

                cht.BlockDataDownloaded(currentChainTip, originalHeaderArray[i].Block);
                cht.PartialValidationSucceeded(currentChainTip, out bool fullValidationRequired);
                cht.ConsensusTipChanged(currentChainTip);
            }

            // All headers should have block data available.
            Dictionary<uint256, ChainedHeader> storedHeaders = cht.GetChainedHeadersByHash();
            foreach (ChainedHeader consumedHeader in consumedHeaderArray)
            {
                storedHeaders.Should().ContainKey(consumedHeader.HashBlock);
                const int heightOfFirstHeaderWithBlockNotNull = initialChainSize + extensionSize - ChainedHeaderTree.KeepBlockDataForLastBlocks;

                storedHeaders[consumedHeader.HashBlock].BlockDataAvailability.Should().Be(BlockDataAvailabilityState.BlockAvailable);
                if (consumedHeader.Height < heightOfFirstHeaderWithBlockNotNull)
                {
                    storedHeaders[consumedHeader.HashBlock].Block.Should().BeNull();
                }
                else
                {
                    storedHeaders[consumedHeader.HashBlock].Block.Should().NotBeNull();
                }
            }
        }

        /// <summary>
        /// Issue 51 @ Call PartialValidationSucceeded on a chained header which doesn't have the block data but it is
        /// in the tree, make sure PartialValidationSucceeded returns null and ReorgRequired is false.
        /// </summary>
        [Fact]
        public void CallingPartialValidationSucceeded_ForConnectedHeaderWithNoBlockData_NothingReturnedAndFullValidationIsNotRequired()
        {
            // Chain header tree setup. Initial chain has 5 headers with no blocks.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chain by 4 headers and connect it to CHT.
            // Example: h1=h2=h3=h4=h5=h6=h7=h8=h9.
            const int extensionSize = 4;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);

            // Present headers.
            List<BlockHeader> listOfExtendedHeaders = ctx.ChainedHeaderToList(chainTip, extensionSize);
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(1, listOfExtendedHeaders);
            ChainedHeader consumed = connectionResult.Consumed;

            // Call partial validation on h4 and make sure nothing is returned
            // and full validation is not required.
            List<ChainedHeaderBlock> headers = cht.PartialValidationSucceeded(consumed, out bool fullValidationRequired);
            headers.Should().BeNull();
            fullValidationRequired.Should().BeFalse();
        }

        /// <summary>
        /// Issue 52 @ Call PartialValidationSucceeded on a chained header which prev header has validation state as HeaderOnly,
        /// make sure PartialValidationSucceeded returns null and ReorgRequired is false.
        /// </summary>
        [Fact]
        public void CallingPartialValidationSucceeded_FoHeaderWithPreviousHeaderWithValidationStateHeaderOnly_NothingReturnedAndFullValidationIsNotRequired()
        {
            // Chain header tree setup. Initial chain has 5 headers with no blocks.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chain by 4 headers and connect it to CHT.
            // Example: h1=h2=h3=h4=h5=h6=h7=h8=h9.
            const int extensionSize = 4;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);

            // Present headers and download them.
            List<BlockHeader> listOfExtendedHeaders = ctx.ChainedHeaderToList(chainTip, extensionSize);
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(1, listOfExtendedHeaders);
            ChainedHeader consumed = connectionResult.Consumed;

            ChainedHeader[] originalHeaderArray = chainTip.ToArray(extensionSize);
            ChainedHeader[] headerArray = consumed.ToArray(extensionSize);
            for (int i = 0; i < headerArray.Length; i++)
            {
                cht.BlockDataDownloaded(headerArray[i], originalHeaderArray[i].Block);
            }

            // Call partial validation on h9 (h8 has validation state as HeaderOnly) and make sure nothing is returned
            // and full validation is not required.
            List<ChainedHeaderBlock> headers = cht.PartialValidationSucceeded(consumed, out bool fullValidationRequired);
            headers.Should().BeNull();
            fullValidationRequired.Should().BeFalse();
        }

        /// <summary>
        /// Issue 53 @ Call PartialValidationSucceeded on a chained header which validation state is PartiallyValid,
        /// make sure PartialValidationSucceeded returns null and ReorgRequired is false.
        /// </summary>
        [Fact]
        public void CallingPartialValidationSucceeded_FoHeaderWithValidationStateAsPartiallyValidated_NothingReturnedAndFullValidationIsNotRequired()
        {
            // Chain header tree setup. Initial chain has 5 headers with no blocks.
            // Example: h1=h2=h3=h4=h5.
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder()
                .WithInitialChain(initialChainSize)
                .Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader chainTip = ctx.InitialChainTip;

            // Extend chain by 4 headers and connect it to CHT.
            // Example: h1=h2=h3=h4=h5=h6=h7=h8=h9.
            const int extensionSize = 4;
            chainTip = ctx.ExtendAChain(extensionSize, chainTip);

            // Present headers, download them and partially validate them.
            List<BlockHeader> listOfExtendedHeaders = ctx.ChainedHeaderToList(chainTip, extensionSize);
            ConnectNewHeadersResult connectionResult = cht.ConnectNewHeaders(1, listOfExtendedHeaders);
            ChainedHeader consumed = connectionResult.Consumed;

            ChainedHeader[] originalHeaderArray = chainTip.ToArray(extensionSize);
            ChainedHeader[] headerArray = consumed.ToArray(extensionSize);
            bool fullValidationRequired;
            for (int i = 0; i < headerArray.Length; i++)
            {
                cht.BlockDataDownloaded(headerArray[i], originalHeaderArray[i].Block);
                cht.PartialValidationSucceeded(headerArray[i], out fullValidationRequired);
            }

            // Call partial validation on h9 again and make sure nothing is returned
            // and full validation is not required.
            Dictionary<uint256, ChainedHeader> treeHeaders = cht.GetChainedHeadersByHash();
            List<ChainedHeaderBlock> headers = cht.PartialValidationSucceeded(treeHeaders[chainTip.HashBlock], out fullValidationRequired);
            headers.Should().BeNull();
            fullValidationRequired.Should().BeFalse();
        }

        /// <summary>
        /// Issue 54 @ CT is at 50a.
        /// Finalized height is 40, max reorg is 10.
        /// Some headers are presented (from 20a to 60b, with fork point 40a) by peer 1.
        /// Peer 2 presents 25a to 55c with fork point at 39a.
        /// Headers from peer 1 should be marked for download(41b to 60b).
        /// When peer 2 presents headers exception on ConnectNewHeaders should be thrown.
        /// </summary>
        [Fact]
        public void PresentHeaders_TwoPeersTwoForks_Peer2CausesMaxReorgViolationException()
        {
            const int initialChainSize = 50;
            const int peerOneId = 1;
            const int peerTwoId = 2;

            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // Chain tip is at h50.
            Assert.Equal(initialChainSize, initialChainTip.Height);

            // Setup max reorg of 10.
            const int maxReorg = 10;
            testContext.ChainState.Setup(x => x.MaxReorgLength).Returns(maxReorg);

            // Setup finalized block height to 40.
            const int finalizedBlockHeight = 40;
            testContext.FinalizedBlockMock.Setup(m => m.GetFinalizedBlockInfo()).Returns(new HashHeightPair(uint256.One, finalizedBlockHeight));

            // Peer 1 presents headers from 20a to 60b, with fork point 40a.
            const int heightOfFirstFork = 40;
            const int chainBExtensionBeyondFork = 20; // h41 -> h60
            ChainedHeader tipOfForkOfChainB = testContext.ExtendAChain(chainBExtensionBeyondFork, initialChainTip.GetAncestor(heightOfFirstFork));
            Assert.Equal(60, tipOfForkOfChainB.Height);
            List<BlockHeader> listOfChainBBlockHeaders = testContext.ChainedHeaderToList(tipOfForkOfChainB, 41); // h20 -> h60
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerOneId, listOfChainBBlockHeaders);

            // Headers from Peer 1 should be marked for download (41b to 60b).
            Assert.Equal(connectNewHeadersResult.DownloadFrom.Header, tipOfForkOfChainB.GetAncestor(heightOfFirstFork + 1).Header);
            Assert.Equal(connectNewHeadersResult.DownloadTo.Header, tipOfForkOfChainB.Header);
            Assert.True(connectNewHeadersResult.HaveBlockDataAvailabilityStateOf(BlockDataAvailabilityState.BlockRequired));

            // Peer 2 presents 25a to 55c with fork point at 39a.
            const int heightOfSecondFork = 39;
            const int chainCExtension = 16; // h40 -> h55
            ChainedHeader tipOfForkOfChainC = testContext.ExtendAChain(chainCExtension, initialChainTip.GetAncestor(heightOfSecondFork));
            List<BlockHeader> listOfChainCBlockHeaders = testContext.ChainedHeaderToList(tipOfForkOfChainC, 31); // 25 -> 55
            Assert.Throws<MaxReorgViolationException>(() => chainedHeaderTree.ConnectNewHeaders(peerTwoId, listOfChainCBlockHeaders));
        }

        /// <summary>
        /// Issue 55 @ CT is at 20a. AssumeValid is on 12b.
        /// Peer 1 presents a chain with tip at 15b and fork point at 10a.
        /// 11b,12b should be marked as AV, 13b,14b,15b should be header only.
        /// No headers from chain b are requested for download.
        /// </summary>
        [Fact]
        public void PresentHeaders_AssumeValidBelowAVBlock_HeadersOnlyAboveAVBlock_NoHeadersRequestedForDownloaded()
        {
            const int initialChainSizeOfTwenty = 20;
            const int chainExtensionSizeOfFive = 5;
            const int assumeValidBlockHeightOfTwelve = 12;

            const int forkHeight = 10;
            const int peerOneId = 1;

            // Chain header tree setup.
            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSizeOfTwenty).UseCheckpoints(false).Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            // Chain tip is at 20.
            Assert.Equal(initialChainSizeOfTwenty, initialChainTip.Height);

            ChainedHeader tipOfChainBFork = testContext.ExtendAChain(chainExtensionSizeOfFive, initialChainTip.GetAncestor(forkHeight));

            // AssumeValid is on 12b.
            testContext.ConsensusSettings.BlockAssumedValid = tipOfChainBFork.GetAncestor(assumeValidBlockHeightOfTwelve).HashBlock;

            // Peer 1 presents a chain with tip at 15b and fork point at 10a.
            List<BlockHeader> listOfExtendedChainBlockHeaders = testContext.ChainedHeaderToList(tipOfChainBFork, tipOfChainBFork.Height - forkHeight + 1 /* fork inclusive */);
            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(peerOneId, listOfExtendedChainBlockHeaders);

            // 11b, 12b should be marked as AV.
            Assert.True(connectNewHeadersResult.Consumed.GetAncestor(11).IsAssumedValid);
            Assert.True(connectNewHeadersResult.Consumed.GetAncestor(12).IsAssumedValid);

            // 13b, 14b, 15b should be header only.
            connectNewHeadersResult.Consumed.GetAncestor(13).BlockValidationState.Should().Be(ValidationState.HeaderValidated);
            connectNewHeadersResult.Consumed.GetAncestor(14).BlockValidationState.Should().Be(ValidationState.HeaderValidated);
            connectNewHeadersResult.Consumed.GetAncestor(15).BlockValidationState.Should().Be(ValidationState.HeaderValidated);

            // No headers from chain b are requested for download.
            connectNewHeadersResult.DownloadFrom.Should().Be(null);
            connectNewHeadersResult.DownloadTo.Should().Be(null);

            // Block availability state for 11b – 15b is header only.
            ChainedHeader chainedHeader = connectNewHeadersResult.Consumed;
            while (chainedHeader.Height > connectNewHeadersResult.Consumed.GetAncestor(11).Height)
            {
                chainedHeader.BlockDataAvailability.Should().Be(BlockDataAvailabilityState.HeaderOnly);
                chainedHeader = chainedHeader.Previous;
            }
        }

        /// <summary>
        /// There are several checkpoints. Headers are presented with some of them.
        /// Headers up to the last checkpointed one that was included in the presented headers should be marked for download.
        /// </summary>
        [Fact]
        public void PresentHeadersMessageWithSeveralCheckpointedHeaders_MarkToDownloadFromStartToLastCheckpointedHeaderInMessage()
        {
            const int initialChainSize = 5;
            const int presentHeadersCount = 2000;
            const int totalHeadersCount = 5000;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            ChainedHeader extendedChainTip = testContext.ExtendAChain(totalHeadersCount, initialChainTip);

            List<BlockHeader> headers = testContext.ChainedHeaderToList(extendedChainTip, initialChainSize + totalHeadersCount);

            List<BlockHeader> headersToPresent = headers.Take(presentHeadersCount).ToList();

            // Setup checkpoints.
            var checkpointsHeight = new List<int>() { 300, 800, 1400, 2300, 4500 };
            var checkpoints = new List<CheckpointFixture>();
            foreach (int checkpointHeight in checkpointsHeight)
                checkpoints.Add(new CheckpointFixture(checkpointHeight, headers[checkpointHeight - 1]));

            testContext.SetupCheckpoints(checkpoints.ToArray());

            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, headersToPresent);

            Assert.Equal(headersToPresent.Last().GetHash(), connectNewHeadersResult.Consumed.HashBlock);
            Assert.Equal(headers[initialChainSize].GetHash(), connectNewHeadersResult.DownloadFrom.HashBlock);
            Assert.Equal(headers[1400 - 1].GetHash(), connectNewHeadersResult.DownloadTo.HashBlock);
        }

        /// <summary>
        /// There are several checkpoints. Headers are presented with several checkpointed headers in one message including last one.
        /// </summary>
        [Fact]
        public void PresentHeadersMessageWithSeveralCheckpointsAndTheLastOne_MarkToDownloadFromStartToLastPresented()
        {
            const int initialChainSize = 5;
            const int presentHeadersCount = 2000;
            const int totalHeadersCount = 5000;

            TestContext testContext = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;
            ChainedHeader initialChainTip = testContext.InitialChainTip;

            ChainedHeader extendedChainTip = testContext.ExtendAChain(totalHeadersCount, initialChainTip);

            List<BlockHeader> headers = testContext.ChainedHeaderToList(extendedChainTip, initialChainSize + totalHeadersCount);

            List<BlockHeader> headersToPresent = headers.Take(presentHeadersCount).ToList();

            // Setup checkpoints.
            var checkpointsHeight = new List<int>() { 300, 800, 1400 };
            var checkpoints = new List<CheckpointFixture>();
            foreach (int checkpointHeight in checkpointsHeight)
                checkpoints.Add(new CheckpointFixture(checkpointHeight, headers[checkpointHeight - 1]));

            testContext.SetupCheckpoints(checkpoints.ToArray());

            ConnectNewHeadersResult connectNewHeadersResult = chainedHeaderTree.ConnectNewHeaders(1, headersToPresent);

            Assert.Equal(headersToPresent.Last().GetHash(), connectNewHeadersResult.Consumed.HashBlock);
            Assert.Equal(headers[initialChainSize].GetHash(), connectNewHeadersResult.DownloadFrom.HashBlock);
            Assert.Equal(headersToPresent.Last().GetHash(), connectNewHeadersResult.DownloadTo.HashBlock);
        }

        [Fact]
        public void PeerPresentedSameHeadersTwiceMakeSurePeerTipIequalToLastHeader()
        {
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;
            ChainedHeaderBlock consensusTip = cht.GetChainedHeaderBlock(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId]);

            ChainedHeader peerChainTip = ctx.ExtendAChain(20, initialChainTip);

            List<BlockHeader> peerHeaders = ctx.ChainedHeaderToList(peerChainTip, peerChainTip.Height);

            cht.ConnectNewHeaders(1, peerHeaders);
            ConnectNewHeadersResult result = cht.ConnectNewHeaders(1, peerHeaders);

            Assert.Equal(cht.GetPeerTipChainedHeaderByPeerId(1).HashBlock, result.Consumed.HashBlock);

            this.CheckChainedHeaderTreeConsistency(cht, ctx, consensusTip, new HashSet<int>() { 1 });
        }

        [Fact]
        public void TreeIsConsistentAfterPeerDisconnected()
        {
            const int initialChainSize = 5;
            TestContext ctx = new TestContextBuilder().WithInitialChain(initialChainSize).UseCheckpoints(false).Build();
            ChainedHeaderTree cht = ctx.ChainedHeaderTree;
            ChainedHeader initialChainTip = ctx.InitialChainTip;
            ChainedHeaderBlock consensusTip = cht.GetChainedHeaderBlock(cht.GetPeerTipsByPeerId()[ChainedHeaderTree.LocalPeerId]);

            ChainedHeader peerChainTip = ctx.ExtendAChain(20, initialChainTip);

            List<BlockHeader> peerHeaders = ctx.ChainedHeaderToList(peerChainTip, peerChainTip.Height);

            cht.ConnectNewHeaders(1, peerHeaders);
            cht.PeerDisconnected(1);

            this.CheckChainedHeaderTreeConsistency(cht, ctx, consensusTip, new HashSet<int>() { });
        }
    }
}