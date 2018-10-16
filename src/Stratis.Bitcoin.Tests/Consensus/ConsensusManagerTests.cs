using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ConsensusManagerTests
    {
        // https://github.com/stratisproject/StratisBitcoinFullNode/issues/1937

        [Fact(Skip = "To be finished")]
        public void BlockMined_PartialValidationOnly_Succeeded_Consensus_TipUpdated()
        {
            TestContext builder = new TestContextBuilder().WithInitialChain(10).BuildOnly();
            ChainedHeader chainTip = builder.InitialChainTip;

            // builder.ConsensusRulesEngine.Setup(c => c.GetBlockHashAsync()).Returns(Task.FromResult(chainTip.HashBlock));
            builder.coinView.UpdateTipHash(chainTip.Header.GetHash());
            builder.ConsensusManager.InitializeAsync(chainTip).GetAwaiter().GetResult();

            var minedBlock = builder.CreateBlock(chainTip);
            var result = builder.ConsensusManager.BlockMinedAsync(minedBlock).GetAwaiter().GetResult();
            Xunit.Assert.NotNull(result);
            Xunit.Assert.Equal(minedBlock.GetHash(), builder.ConsensusManager.Tip.HashBlock);
        }


        [Fact]
        public void HeadersPresented_NewHeaders_ProducesExpectedBlocks()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10);
            TestContext builder = contextBuilder.Build();

            builder.SetupAverageBlockSize(200);
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.True(builder.ConsensusManager.PeerIsKnown(peer.Object.Connection.Id));
            Assert.Equal(200 * 10, builder.ConsensusManager.GetExpectedBlockDataBytes());
            var blockSize = builder.ConsensusManager.GetExpectedBlockSizes();
            var expectedBlocks = headerTree.Select(h => h.GetHash()).ToList();
            AssertBlockSizes(blockSize, expectedBlocks, 200);
        }

        [Fact(Skip = "Does not work. Needs to been reviewed if possible to fix or test case is incorrect.")]
        public void HeadersPresented_NewHeaders_BlockSizeTotalHigherThanMaxUnconsumedBlocksDataBytes_UnexpectedlyBiggerBlocksThanAverage_LimitsDownloadedBlocks()
        {
            var contextBuilder = new TestContextBuilder().UseCheckpoints(false).WithInitialChain(10);
            TestContext builder = contextBuilder.Build();

            builder.SetupAverageBlockSize(200);
            builder.ConsensusManager.SetMaxUnconsumedBlocksDataBytes(1000);
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(3, builder.InitialChainTip, avgBlockSize: 500);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 3);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.True(builder.ConsensusManager.PeerIsKnown(peer.Object.Connection.Id));
            Assert.Equal(200 * 2, builder.ConsensusManager.GetExpectedBlockDataBytes());
            var blockSize = builder.ConsensusManager.GetExpectedBlockSizes();
            // expect only first two blocks.
            var expectedBlocks = headerTree.Take(2).Select(h => h.GetHash()).ToList();
            AssertBlockSizes(blockSize, expectedBlocks, 500);
        }

        [Fact]
        public void HeadersPresented_NewHeaders_BlockSizeTotalHigherThanMaxUnconsumedBlocksDataBytes_LimitsDownloadedBlocks()
        {
            var contextBuilder = new TestContextBuilder().UseCheckpoints(false).WithInitialChain(10);
            TestContext builder = contextBuilder.Build();

            builder.SetupAverageBlockSize(200);
            builder.ConsensusManager.SetMaxUnconsumedBlocksDataBytes(1000);
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip, avgBlockSize: 200);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.True(builder.ConsensusManager.PeerIsKnown(peer.Object.Connection.Id));
            Assert.Equal(200 * 5, builder.ConsensusManager.GetExpectedBlockDataBytes());
            var blockSize = builder.ConsensusManager.GetExpectedBlockSizes();
            // expect only first five blocks.
            var expectedBlocks = headerTree.Take(5).Select(h => h.GetHash()).ToList();
            AssertBlockSizes(blockSize, expectedBlocks, 200);
        }

        [Fact]
        public void HeadersPresented_DownloadHeaders_ReturnsCorrectHeaderResult()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.Equal(headerTree.Last().GetHash(), result.Consumed.Header.GetHash());
            Assert.Equal(headerTree.First().GetHash(), result.DownloadFrom.Header.GetHash());
            Assert.Equal(headerTree.Last().GetHash(), result.DownloadTo.Header.GetHash());
        }

        [Fact]
        public void HeadersPresented_NoTriggerDownload_ReturnsCorrectHeaderResult()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, false);

            builder.VerifyNoBlocksAskedToBlockPuller();
            Assert.Equal(headerTree.Last().GetHash(), result.Consumed.Header.GetHash());
            Assert.Equal(headerTree.First().GetHash(), result.DownloadFrom.Header.GetHash());
            Assert.Equal(headerTree.Last().GetHash(), result.DownloadTo.Header.GetHash());
        }

        private static void AssertBlockSizes(Dictionary<uint256, long> blockSize, List<uint256> expectedBlocks, int expectedSize)
        {
            foreach (var hash in expectedBlocks)
            {
                // checks it exists and is expectedsize at the same time.
                Assert.Equal(expectedSize, blockSize[hash]);
            }
        }
    }
}