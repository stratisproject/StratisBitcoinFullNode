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

        public class TestContextBuilder
        {
            private readonly TestContext testContext;

            public TestContextBuilder()
            {
                this.testContext = new TestContext();
            }

            internal TestContextBuilder WithInitialChain(int initialChainSize)
            {
                if (initialChainSize < 0)
                    throw new ArgumentOutOfRangeException(nameof(initialChainSize), "Size cannot be less than 0.");

                this.testContext.InitialChainTip = this.testContext.ExtendAChain(initialChainSize);
                return this;
            }

            internal TestContextBuilder UseCheckpoints(bool useCheckpoints = true)
            {
                this.testContext.ConsensusSettings.UseCheckpoints = useCheckpoints;
                return this;
            }

            internal TestContext Build()
            {
                if (this.testContext.InitialChainTip != null)
                    this.testContext.ChainedHeaderTree.Initialize(this.testContext.InitialChainTip, true);

                return this.testContext;
            }
        }

        public class TestContext
        {
            public Network Network = Network.RegTest;
            public Mock<IBlockValidator> ChainedHeaderValidatorMock = new Mock<IBlockValidator>();
            public Mock<ICheckpoints> CheckpointsMock = new Mock<ICheckpoints>();
            public Mock<IChainState> ChainStateMock = new Mock<IChainState>();
            public Mock<IFinalizedBlockHeight> FinalizedBlockMock = new Mock<IFinalizedBlockHeight>();
            public ConsensusSettings ConsensusSettings = new ConsensusSettings(new NodeSettings(Network.RegTest));

            private static int nonceValue;

            internal ChainedHeaderTree ChainedHeaderTree;

            internal ChainedHeader InitialChainTip;

            public TestContext()
            {
                this.ChainedHeaderTree = new ChainedHeaderTree(
                    this.Network,
                    new ExtendedLoggerFactory(),
                    this.ChainedHeaderValidatorMock.Object,
                    this.CheckpointsMock.Object,
                    this.ChainStateMock.Object,
                    this.FinalizedBlockMock.Object,
                    this.ConsensusSettings);
            }

            internal Target ChangeDifficulty(ChainedHeader header, int difficultyAdjustmentDivisor)
            {
                BigInteger newTarget = header.Header.Bits.ToBigInteger();
                newTarget = newTarget.Divide(BigInteger.ValueOf(difficultyAdjustmentDivisor));
                return new Target(newTarget);
            }

            public void SetupCheckpoints(params CheckpointFixture[] checkpoints)
            {
                if (checkpoints.GroupBy(h => h.Height).Any(g => g.Count() > 1))
                    throw new ArgumentException("Checkpoint heights must be unique.");

                if (checkpoints.Any(h => h.Height < 0))
                    throw new ArgumentException("Checkpoint heights cannot be negative.");

                foreach (CheckpointFixture checkpoint in checkpoints.OrderBy(h => h.Height))
                {
                    var checkpointInfo = new CheckpointInfo(checkpoint.Header.GetHash());
                    this.CheckpointsMock
                        .Setup(c => c.GetCheckpoint(checkpoint.Height))
                        .Returns(checkpointInfo);
                }

                this.CheckpointsMock
                    .Setup(c => c.GetCheckpoint(It.IsNotIn(checkpoints.Select(h => h.Height))))
                    .Returns((CheckpointInfo)null);
                this.CheckpointsMock
                    .Setup(c => c.GetLastCheckpointHeight())
                    .Returns(checkpoints.OrderBy(h => h.Height).Last().Height);
            }

            public ChainedHeader ExtendAChain(int count, ChainedHeader chainedHeader = null, int difficultyAdjustmentDivisor = 1, ValidationState? validationState = null)
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
                    if (validationState.HasValue)
                        newHeader.BlockValidationState = validationState.Value;
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
            TestContext testContext = new TestContextBuilder().Build();
            ChainedHeaderTree chainedHeaderTree = testContext.ChainedHeaderTree;

            Assert.Throws<ConnectHeaderException>(() => chainedHeaderTree.ConnectNewHeaders(1, new List<BlockHeader>(new[] { testContext.Network.GetGenesis().Header })));
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

            // Set chain A tip as a consensus tip.
            cht.ConsensusTipChanged(chainATip);

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

            // Chain B is presented by peer 2. It doesn't meet "assume valid" hash but should still
            // be marked for a download.
            connectNewHeadersResult = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            chainedHeaderDownloadTo = connectNewHeadersResult.DownloadTo;
            chainedHeaderDownloadTo.HashBlock.Should().Be(chainBTip.HashBlock);
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

            // Chain B is presented by peer 2.
            // DownloadFrom should be set to header 5. 
            // DownloadTo should be set to header 10. 
            result = cht.ConnectNewHeaders(2, listOfChainBBlockHeaders);
            result.DownloadFrom.HashBlock.Should().Be(listOfChainBBlockHeaders[checkpointHeight].GetHash());
            result.DownloadTo.HashBlock.Should().Be(listOfChainBBlockHeaders.Last().GetHash());
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
            List<BlockHeader> listOfCurrentChainHeaders =
                ctx.ChainedHeaderToList(chainTip, partiallyValidatedHeadersCount);
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
            listOfCurrentChainHeaders = 
                    ctx.ChainedHeaderToList(chainTip, extensionHeadersCount);
            
            // Chain is presented by peer 1.
            result = cht.ConnectNewHeaders(1, listOfCurrentChainHeaders);

            // Headers h7-h9 are marked as "assumed valid".
            ChainedHeader consumed = result.Consumed;
            var expectedState = ValidationState.HeaderValidated;
            while (consumed.Height > initialChainSize)
            {
                if (consumed.Height == 9) expectedState = ValidationState.AssumedValid;
                if (consumed.Height == 6) expectedState = ValidationState.PartiallyValidated;
                consumed.BlockValidationState.Should().Be(expectedState);
                consumed = consumed.Previous;
            }
        }

        /// <summary>
        /// Issue 21 @ FindHeaderAndVerifyBlockIntegrity called for some bogus block. Should throw because not connected.
        /// </summary>
        [Fact]
        public void FindHeaderAndVerifyBlockIntegrityCalledForBogusBlock_ExceptionShouldBeThrown()
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

            // Call FindHeaderAndVerifyBlockIntegrity on the block from header 6.
            // BlockDownloadedForMissingChainedHeaderException should be thrown.
            Action verificationAction = () => cht.FindHeaderAndVerifyBlockIntegrity(initialChainTip.Block);
            verificationAction.Should().Throw<BlockDownloadedForMissingChainedHeaderException>();
        }
    }
}