using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Moq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
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

            private static int nonceValue;

            internal ChainedHeaderTree ChainedHeaderTree;

            internal ChainedHeaderTree CreateChainedHeaderTree()
            {
                this.ChainedHeaderTree = new ChainedHeaderTree(this.Network, new ExtendedLoggerFactory(), this.ChainedHeaderValidatorMock.Object, this.CheckpointsMock.Object, this.ChainStateMock.Object, this.FinalizedBlockMock.Object, this.ConsensusSettings);
                return this.ChainedHeaderTree;
            }

            internal Target ChangeDifficulty(ChainedHeader header, int difficultyAdjustmentDivisor)
            {
                BigInteger newTarget = header.Header.Bits.ToBigInteger();
                newTarget = newTarget.Divide(BigInteger.ValueOf(difficultyAdjustmentDivisor));
                return new Target(newTarget);
            }

            public ChainedHeader ExtendAChain(int count, ChainedHeader chainedHeader = null, int difficultyAdjustmentDivisor = 1)
            {
                if (difficultyAdjustmentDivisor == 0) throw new ArgumentException("Divisor cannot be 0");

                ChainedHeader previousHeader = chainedHeader ?? new ChainedHeader(this.Network.GetGenesis().Header, this.Network.GenesisHash, 0);

                for (int i = 0; i < count; i++)
                {
                    BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                    header.HashPrevBlock = previousHeader.HashBlock;
                    header.Bits = difficultyAdjustmentDivisor == 1
                                        ? previousHeader.Header.Bits
                                        : this.ChangeDifficulty(previousHeader, difficultyAdjustmentDivisor);
                    header.Nonce = (uint)Interlocked.Increment(ref nonceValue);
                    var newHeader = new ChainedHeader(header, header.GetHash(), previousHeader);
                    Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                    block.GetSerializedSize();
                    newHeader.Block = block;
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
            var testContext = new TestContext();
            ChainedHeaderTree chainedHeaderTree = testContext.CreateChainedHeaderTree();

            Assert.Throws<ConnectHeaderException>(() => chainedHeaderTree.ConnectNewHeaders(1, new List<BlockHeader>(new[] { testContext.Network.GetGenesis().Header })));
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
            List<BlockHeader> peer1Headers = listOfNewBlockHeaders.GetRange(0, 3);
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
        /// Issue 13 @ Create 2 chains - chain A and chain B, where chain A has more chain work than chain B. Connect both
        /// chains to chain header tree. Consensus tip should be set to chain A. Now extend / update chain B to make it have
        /// more chain work. Attempt to connect chain B again. Consensus tip should be set to chain B.
        /// </summary>
        [Fact]
        public void PresentDifferentChains_AlternativeChainWithMoreChainWorkShouldAlwaysBeMarkedForDownload()
        {
            // Chain header tree setup.
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader initialChainTip = ctx.ExtendAChain(5);
            cht.Initialize(initialChainTip, true);
            ctx.ConsensusSettings.UseCheckpoints = false;

            // Chains A and B setup.
            const int commonChainSize = 4;
            const int chainAExtension = 4;
            const int chainBExtension = 2;
            ChainedHeader commonChainTip = ctx.ExtendAChain(commonChainSize, initialChainTip); // ie. h1=h2=h3=h4
            ChainedHeader chainATip = ctx.ExtendAChain(chainAExtension, commonChainTip); // ie. (h1=h2=h3=h4)=a5=a6=a7=a8
            ChainedHeader chainBTip = ctx.ExtendAChain(chainBExtension, commonChainTip); // ie. (h1=h2=h3=h4)=b5=b6
            List<BlockHeader> listOfChainABlockHeaders = ctx.ChainedHeaderToList(chainATip, commonChainSize + chainAExtension);
            List<BlockHeader> listOfChainBBlockHeaders = ctx.ChainedHeaderToList(chainBTip, commonChainSize + chainBExtension);

            // Chain A is presented by peer 1. DownloadTo should be chain A tip.
            ConnectNewHeadersResult connectNewHeadersResult = cht.ConnectNewHeaders(1, listOfChainABlockHeaders);
            ChainedHeader chainedHeaderTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderTo.HashBlock.Should().Be(chainATip.HashBlock);

            // Set chain A tip as a consensus tip.
            cht.ConsensusTipChanged(chainATip);

            // Chain B is presented by peer 2. DownloadTo should be not set, as chain
            // B has less chain work.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            connectNewHeadersResult.DownloadTo.Should().BeNull();

            // Add more chain work and blocks into chain B.
            const int chainBAdditionalBlocks = 4;
            chainBTip = ctx.ExtendAChain(chainBAdditionalBlocks, chainBTip); // ie. (h1=h2=h3=h4)=b5=b6=b7=b8=b9=b10
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
        }

        /// <summary>
        /// Issue 14 @ Chain exists with checkpoints enabled. There are 2 checkpoints. Peer presents a chain that covers
        /// first checkpoint with a prolongation that does not match the 2nd checkpoint. Exception should be thrown.
        /// </summary>
        [Fact]
        public void ChainHasTwoCheckPoints_ChainCoveringOnlyFirstCheckPointIsPresented_ChainIsDiscardedUpUntilFirstCheckpoint()
        {
            // Chain header tree setup.
            const int initialChainSize = 2;
            const int currentChainExtension = 6;
            var ctx = new TestContext();
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader initialChainTip = ctx.ExtendAChain(initialChainSize); // ie. h1=h2
            cht.Initialize(initialChainTip, true); 
            ChainedHeader extendedChainTip = ctx.ExtendAChain(currentChainExtension, initialChainTip); // ie. h1=h2=h3=h4=h5=h6=h7=h8
            ctx.ConsensusSettings.UseCheckpoints = true;
            List<BlockHeader> listOfCurrentChainHeaders = ctx.ChainedHeaderToList(extendedChainTip, initialChainSize + currentChainExtension);

            // Setup two known checkpoints at header 4 and 7.
            // Example: h1=h2=h3=(h4)=h5=h6=(h7)=h8.
            const int firstCheckpointHeight = 4;
            const int secondCheckpointHeight = 7;
            var checkpoint1 = new CheckpointInfo(listOfCurrentChainHeaders[firstCheckpointHeight - 1].GetHash());
            var checkpoint2 = new CheckpointInfo(listOfCurrentChainHeaders[secondCheckpointHeight - 1].GetHash());
            ctx.CheckpointsMock
               .Setup(c => c.GetCheckpoint(firstCheckpointHeight))
               .Returns(checkpoint1);
            ctx.CheckpointsMock
               .Setup(c => c.GetCheckpoint(secondCheckpointHeight))
               .Returns(checkpoint2);
            ctx.CheckpointsMock
               .Setup(c => c.GetCheckpoint(It.IsNotIn(firstCheckpointHeight, secondCheckpointHeight)))
               .Returns((CheckpointInfo)null);
            ctx.CheckpointsMock
                .Setup(c => c.GetLastCheckpointHeight())
                .Returns(secondCheckpointHeight);

            // Setup new chain that only covers first checkpoint but doesn't cover second checkpoint.
            // Example: h1=h2=h3=(h4)=h5=h6=x7=x8=x9=x10.
            const int newChainExtension = 4;
            extendedChainTip = extendedChainTip.Previous; // walk back to block 6
            extendedChainTip = extendedChainTip.Previous;
            extendedChainTip = ctx.ExtendAChain(newChainExtension, extendedChainTip); 
            List<BlockHeader> listOfNewChainHeaders = ctx.ChainedHeaderToList(extendedChainTip, extendedChainTip.Height);

            // First 5 blocks are presented by peer 1.
            // DownloadTo should be set to a checkpoint 1. 
            ConnectNewHeadersResult result = cht.ConnectNewHeaders(1, listOfNewChainHeaders.Take(5).ToList());
            result.DownloadTo.HashBlock.Should().Be(checkpoint1.Hash);

            // Remaining 5 blocks are presented by peer 1 which do not cover checkpoint 2.
            // InvalidHeaderException should be thrown.
            Action connectAction = () =>
            {
                cht.ConnectNewHeaders(1, listOfNewChainHeaders.Skip(5).ToList());
            };

            connectAction.Should().Throw<InvalidHeaderException>();
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
            // Initial chain has 2 blocks.
            // Example: h1=h2.
            var ctx = new TestContext();
            const int initialChainSize = 2;
            ChainedHeaderTree cht = ctx.CreateChainedHeaderTree();
            ChainedHeader initialChainTip = ctx.ExtendAChain(initialChainSize);
            cht.Initialize(initialChainTip, true);
            ctx.ConsensusSettings.UseCheckpoints = false;

            // Setup two alternative chains A and B of the same length.
            const int presentedChainSize = 4;
            ChainedHeader chainATip = ctx.ExtendAChain(presentedChainSize, initialChainTip); // ie. h1=h2=a1=a2=a3=a4
            ChainedHeader chainBTip = ctx.ExtendAChain(presentedChainSize, initialChainTip); // ie. h1=h2=b1=b2=b3=b4
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
            
            // Chain B is presented by peer 2. It doesn't meet "assume valid" hash but should still
            // be marked for a download.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            chainedHeaderDownloadTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderDownloadTo.HashBlock.Should().Be(chainBTip.HashBlock);
        }
    }
}