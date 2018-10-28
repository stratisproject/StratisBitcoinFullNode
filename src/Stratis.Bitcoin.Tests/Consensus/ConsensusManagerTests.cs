using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
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

        [Fact]
        public void ProcessDownloadedBlock_PartialValidationCalledWhenBlockImmediatelyAfterTip()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            builder.PartialValidator.Verify(p => p.StartPartialValidation(It.IsAny<ChainedHeader>(), It.IsAny<Block>(), It.IsAny<OnPartialValidationCompletedAsyncCallback>()), Times.Exactly(1));
        }

        [Fact]
        public void ProcessDownloadedBlock_PartialValidationNotCalledWhenBlockNotImmediatelyAfterTip()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(2, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 2).ToList();
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            builder.PartialValidator.Verify(p => p.StartPartialValidation(It.IsAny<ChainedHeader>(), It.IsAny<Block>(), It.IsAny<OnPartialValidationCompletedAsyncCallback>()), Times.Exactly(0));
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_UnknownHeader_BlockNotExpected_ThrowsInvalidOperationException()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            Assert.Throws<InvalidOperationException>(() => builder.blockPullerBlockDownloadCallback(new uint256(29836872365), null, peer.Object.Connection.Id));

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_KnownHeader_BlockExpected_CallbackNotRegistered_CallbackNotCalled()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var additionalHeaders2 = builder.ExtendAChain(1, additionalHeaders);
            builder.ConsensusManager.AddExpectedBlockSize(additionalHeaders2.HashBlock, additionalHeaders2.Block.BlockSize.Value);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            builder.blockPullerBlockDownloadCallback(additionalHeaders2.HashBlock, null, peer.Object.Connection.Id);

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_KnownHeader_NotBlockExpected_ThrowsInvalidOperationException()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();


            // todo: either load and clear or not load at all.
            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);
            builder.ConsensusManager.ClearExpectedBlockSizes();

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            Assert.Throws<InvalidOperationException>(() => builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, null, peer.Object.Connection.Id));

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_KnownHeader_BlockExpected_CallbackRegistered_CallbackCalled()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            // expect the setup callback is not called.
            Assert.True(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_KnownHeader_BlockIntegrityInvalidated_BansPeer_DoesNotCallCallback()
        {
            var contextBuilder = new TestContextBuilder(KnownNetworks.StratisMain).WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);

            // setup validation to fail.
            builder.IntegrityValidator.Setup(i => i.VerifyBlockIntegrity(additionalHeaders, additionalHeaders.Block))
                .Returns(new Bitcoin.Consensus.ValidationContext()
                {
                    BanDurationSeconds = 3000,
                    Error = Bitcoin.Consensus.ConsensusErrors.BadBlockSignature
                });

            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
            builder.AssertPeerBanned(peer.Object);
            builder.AssertExpectedBlockSizesEmpty();
        }

        [Fact]
        public void BlockDownloaded_ExpectedBlockDataBytesCalculatedCorrectly()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            builder.SetupAverageBlockSize(100);
            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip, avgBlockSize: 100);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);

            builder.ConsensusManager.SetExpectedBlockDataBytes(1000);

            var callback1Called = false;
            var callback2Called = false;
            var callback1 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback1Called = true; });
            var callback2 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback2Called = true; });

            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback1, callback2);

            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);


            Assert.False(builder.ConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(additionalHeaders.HashBlock));
            Assert.Equal(900, builder.ConsensusManager.GetExpectedBlockDataBytes());
            builder.AssertExpectedBlockSizesEmpty();
            Assert.True(callback1Called);
            Assert.True(callback2Called);
        }

        [Fact]
        public void BlockDownloaded_NullBlock_CallsCallbacks()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(10).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(2, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 2);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);

            builder.ConsensusManager.SetExpectedBlockDataBytes(1000);

            var callback1Called = false;
            var callback2Called = false;
            var callback3Called = false;
            ChainedHeaderBlock calledWith1 = null;
            ChainedHeaderBlock calledWith2 = null;
            ChainedHeaderBlock calledWith3 = null;
            var callback1 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback1Called = true; calledWith1 = d; });
            var callback2 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback2Called = true; calledWith2 = d; });
            var callback3 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback3Called = true; calledWith3 = d; });

            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback1, callback2);
            builder.ConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.Previous.HashBlock, callback3);

            // call for both blocks.
            builder.blockPullerBlockDownloadCallback(additionalHeaders.Previous.HashBlock, null, peer.Object.Connection.Id);
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, null, peer.Object.Connection.Id);

            Assert.False(builder.ConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(additionalHeaders.HashBlock));
            Assert.False(builder.ConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(additionalHeaders.Previous.HashBlock));

            builder.AssertExpectedBlockSizesEmpty();

            Assert.True(callback1Called);
            Assert.True(callback2Called);
            Assert.True(callback3Called);
            Assert.Null(calledWith1);
            Assert.Null(calledWith2);
            Assert.Null(calledWith3);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockNotInCT_ReturnsNull()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            var result = builder.ConsensusManager.GetBlockDataAsync(new uint256(234)).GetAwaiter().GetResult();

            Assert.Null(result);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockInCT_HasBlock_ReturnsBlock()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var result = builder.ConsensusManager.GetBlockDataAsync(builder.InitialChainTip.HashBlock).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.IsType<ChainedHeaderBlock>(result);
            Assert.Equal(builder.InitialChainTip.Block, result.Block);
            Assert.Equal(builder.InitialChainTip, result.ChainedHeader);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockInCT_HasNoBlock_BlockInBlockStore_ReturnsBlockFromBlockStore()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            var initialChainTipBlock = builder.InitialChainTip.Block;
            builder.InitialChainTip.Block = null;
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            builder.BlockStore.Setup(g => g.GetBlockAsync(builder.InitialChainTip.HashBlock))
             .ReturnsAsync(() =>
             {
                 return initialChainTipBlock;
             });

            var result = builder.ConsensusManager.GetBlockDataAsync(builder.InitialChainTip.HashBlock).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.IsType<ChainedHeaderBlock>(result);
            Assert.Equal(initialChainTipBlock, result.Block);
            Assert.Equal(builder.InitialChainTip, result.ChainedHeader);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockInCT_HasNoBlock_BlockNotInBlockStore_ReturnsChainedHeaderBlockFromCT()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();

            builder.InitialChainTip.Block = null;
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            builder.BlockStore.Setup(g => g.GetBlockAsync(builder.InitialChainTip.HashBlock))
                .ReturnsAsync(() =>
                {
                    return null;
                });

            var result = builder.ConsensusManager.GetBlockDataAsync(builder.InitialChainTip.HashBlock).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.IsType<ChainedHeaderBlock>(result);
            Assert.Null(result.Block);
            Assert.Equal(builder.InitialChainTip, result.ChainedHeader);
        }


        [Fact]
        public void GetOrDownloadBlocksAsync_ChainedHeaderBlockNotInCT_CallsBlockDownloadedCallbackForBlock_BlocksNotDownloaded()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();
            var blockHashes = new List<uint256>()
             {
                builder.InitialChainTip.HashBlock
             };


            var callbackCalled = false;
            ChainedHeaderBlock calledWith = null;
            var blockDownloadedCallback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; calledWith = d; });

            builder.ConsensusManager.GetOrDownloadBlocksAsync(blockHashes, blockDownloadedCallback).GetAwaiter().GetResult();

            Assert.True(callbackCalled);
            Assert.Null(calledWith);
            builder.BlockPuller.Verify(b => b.RequestBlocksDownload(It.IsAny<List<ChainedHeader>>(), It.IsAny<bool>()), Times.Exactly(0));
        }

        [Fact]
        public void GetOrDownloadBlocksAsync_ChainedHeaderBlockInCTWithoutBlock_DoesNotCallBlockDownloadedCallbackForBlock_BlockDownloaded()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();
            var blockHashes = new List<uint256>()
             {
                builder.InitialChainTip.HashBlock
             };

            builder.InitialChainTip.Block = null;
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var callbackCalled = false;
            ChainedHeaderBlock calledWith = null;
            var blockDownloadedCallback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; calledWith = d; });

            builder.ConsensusManager.GetOrDownloadBlocksAsync(blockHashes, blockDownloadedCallback).GetAwaiter().GetResult();

            Assert.True(builder.ConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(builder.InitialChainTip.HashBlock));
            Assert.False(callbackCalled);
            builder.BlockPuller.Verify(b => b.RequestBlocksDownload(It.IsAny<List<ChainedHeader>>(), It.IsAny<bool>()), Times.Exactly(1));
        }

        [Fact]
        public void GetOrDownloadBlocksAsync_ChainedHeaderBlockInCTWithBlock_CallsBlockDownloadedCallbackForBlock_BlockNotDownloaded()
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(3).UseCheckpoints(false);
            TestContext builder = contextBuilder.Build();
            var blockHashes = new List<uint256>()
             {
                builder.InitialChainTip.HashBlock
             };
            
            builder.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var callbackCalled = false;
            ChainedHeaderBlock calledWith = null;
            var blockDownloadedCallback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; calledWith = d; });

            builder.ConsensusManager.GetOrDownloadBlocksAsync(blockHashes, blockDownloadedCallback).GetAwaiter().GetResult();
            
            Assert.True(callbackCalled);
            Assert.NotNull(calledWith);
            Assert.IsType<ChainedHeaderBlock>(calledWith);
            Assert.Equal(builder.InitialChainTip.Block, calledWith.Block);
            Assert.Equal(builder.InitialChainTip, calledWith.ChainedHeader);
            builder.BlockPuller.Verify(b => b.RequestBlocksDownload(It.IsAny<List<ChainedHeader>>(), It.IsAny<bool>()), Times.Exactly(0));
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