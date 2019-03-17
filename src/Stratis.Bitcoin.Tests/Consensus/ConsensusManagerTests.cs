using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Primitives;
using Xunit;

namespace Stratis.Bitcoin.Tests.Consensus
{
    public class ConsensusManagerTests
    {
        [Fact]
        public void HeadersPresented_NewHeaders_ProducesExpectedBlocks()
        {
            TestContext builder = GetBuildTestContext(10);

            builder.SetupAverageBlockSize(200);
            builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.True(builder.TestConsensusManager.PeerIsKnown(peer.Object.Connection.Id));
            Assert.Equal(200 * 10, builder.TestConsensusManager.GetExpectedBlockDataBytes());
            var blockSize = builder.TestConsensusManager.GetExpectedBlockSizes();
            var expectedBlocks = headerTree.Select(h => h.GetHash()).ToList();
            AssertBlockSizes(blockSize, expectedBlocks, 200);
        }

        [Fact(Skip = "Does not work. Needs to been reviewed if possible to fix or test case is incorrect.")]
        public void HeadersPresented_NewHeaders_BlockSizeTotalHigherThanMaxUnconsumedBlocksDataBytes_UnexpectedlyBiggerBlocksThanAverage_LimitsDownloadedBlocks()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);

            builder.SetupAverageBlockSize(200);
            builder.TestConsensusManager.SetMaxUnconsumedBlocksDataBytes(1000);
            builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(3, builder.InitialChainTip, avgBlockSize: 500);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 3);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.True(builder.TestConsensusManager.PeerIsKnown(peer.Object.Connection.Id));
            Assert.Equal(200 * 2, builder.TestConsensusManager.GetExpectedBlockDataBytes());
            var blockSize = builder.TestConsensusManager.GetExpectedBlockSizes();

            // expect only first two blocks.
            var expectedBlocks = headerTree.Take(2).Select(h => h.GetHash()).ToList();
            AssertBlockSizes(blockSize, expectedBlocks, 500);
        }

        [Fact]
        public void HeadersPresented_NewHeaders_BlockSizeTotalHigherThanMaxUnconsumedBlocksDataBytes_LimitsDownloadedBlocks()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);

            builder.SetupAverageBlockSize(200);
            builder.TestConsensusManager.SetMaxUnconsumedBlocksDataBytes(1000);
            builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip, avgBlockSize: 200);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.True(builder.TestConsensusManager.PeerIsKnown(peer.Object.Connection.Id));
            Assert.Equal(200 * 5, builder.TestConsensusManager.GetExpectedBlockDataBytes());
            var blockSize = builder.TestConsensusManager.GetExpectedBlockSizes();

            // expect only first five blocks.
            var expectedBlocks = headerTree.Take(5).Select(h => h.GetHash()).ToList();
            AssertBlockSizes(blockSize, expectedBlocks, 200);
        }

        [Fact]
        public void HeadersPresented_DownloadBlocks_ReturnsCorrectHeaderResult()
        {
            TestContext builder = GetBuildTestContext(10, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.Equal(headerTree.Last().GetHash(), result.Consumed.Header.GetHash());
            Assert.Equal(headerTree.First().GetHash(), result.DownloadFrom.Header.GetHash());
            Assert.Equal(headerTree.Last().GetHash(), result.DownloadTo.Header.GetHash());
        }

        [Fact]
        public void HeadersPresented_NoTriggerDownload_ReturnsCorrectHeaderResult()
        {
            TestContext builder = GetBuildTestContext(10, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(10, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 10);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, false);

            builder.VerifyNoBlocksAskedToBlockPuller();
            Assert.Equal(headerTree.Last().GetHash(), result.Consumed.Header.GetHash());
            Assert.Equal(headerTree.First().GetHash(), result.DownloadFrom.Header.GetHash());
            Assert.Equal(headerTree.Last().GetHash(), result.DownloadTo.Header.GetHash());
        }

        [Fact]
        public void ProcessDownloadedBlock_PartialValidationCalledWhenBlockImmediatelyAfterTip()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            builder.PartialValidator.Verify(p => p.StartPartialValidation(It.IsAny<ChainedHeader>(), It.IsAny<Block>(), It.IsAny<OnPartialValidationCompletedAsyncCallback>()), Times.Exactly(1));
        }

        [Fact]
        public void ProcessDownloadedBlock_PartialValidationNotCalledWhenBlockNotImmediatelyAfterTip()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(2, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 2).ToList();
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            builder.PartialValidator.Verify(p => p.StartPartialValidation(It.IsAny<ChainedHeader>(), It.IsAny<Block>(), It.IsAny<OnPartialValidationCompletedAsyncCallback>()), Times.Exactly(0));
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_UnknownHeader_BlockNotExpected_ThrowsInvalidOperationException()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            Assert.Throws<InvalidOperationException>(() => builder.blockPullerBlockDownloadCallback(new uint256(29836872365), null, peer.Object.Connection.Id));

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_KnownHeader_BlockExpected_CallbackNotRegistered_CallbackNotCalled()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var additionalHeaders2 = builder.ExtendAChain(1, additionalHeaders);
            builder.TestConsensusManager.AddExpectedBlockSize(additionalHeaders2.HashBlock, additionalHeaders2.Block.BlockSize.Value);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            builder.blockPullerBlockDownloadCallback(additionalHeaders2.HashBlock, null, peer.Object.Connection.Id);

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_KnownHeader_NotBlockExpected_ThrowsInvalidOperationException()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);
            builder.TestConsensusManager.ClearExpectedBlockSizes();

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            Assert.Throws<InvalidOperationException>(() => builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, null, peer.Object.Connection.Id));

            // expect the setup callback is not called.
            Assert.False(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_CallbackRegisteredForHash_KnownHeader_BlockExpected_CallbackRegistered_CallbackCalled()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);
            // call the blockdownloaded method from the blockpuller with a random header
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            // expect the setup callback is not called.
            Assert.True(callbackCalled);
        }

        [Fact]
        public void BlockDownloaded_KnownHeader_BlockIntegrityInvalidated_BansPeer_DoesNotCallCallback()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            var callbackCalled = false;
            var callback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; });

            // setup the callback
            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback);

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
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            builder.SetupAverageBlockSize(100);
            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip, avgBlockSize: 100);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);

            builder.TestConsensusManager.SetExpectedBlockDataBytes(1000);

            var callback1Called = false;
            var callback2Called = false;
            var callback1 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback1Called = true; });
            var callback2 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback2Called = true; });

            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback1, callback2);

            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);


            Assert.False(builder.TestConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(additionalHeaders.HashBlock));
            Assert.Equal(900, builder.TestConsensusManager.GetExpectedBlockDataBytes());
            builder.AssertExpectedBlockSizesEmpty();
            Assert.True(callback1Called);
            Assert.True(callback2Called);
        }

        [Fact]
        public void BlockDownloaded_NullBlock_CallsCallbacks()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(2, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 2);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            Assert.NotNull(builder.blockPullerBlockDownloadCallback);

            builder.TestConsensusManager.SetExpectedBlockDataBytes(1000);

            var callback1Called = false;
            var callback2Called = false;
            var callback3Called = false;
            ChainedHeaderBlock calledWith1 = null;
            ChainedHeaderBlock calledWith2 = null;
            ChainedHeaderBlock calledWith3 = null;
            var callback1 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback1Called = true; calledWith1 = d; });
            var callback2 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback2Called = true; calledWith2 = d; });
            var callback3 = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callback3Called = true; calledWith3 = d; });

            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.HashBlock, callback1, callback2);
            builder.TestConsensusManager.SetupCallbackByBlocksRequestedHash(additionalHeaders.Previous.HashBlock, callback3);

            // call for both blocks.
            var block = new Block();
            block.ToBytes();
            builder.blockPullerBlockDownloadCallback(additionalHeaders.Previous.HashBlock, block, peer.Object.Connection.Id);
            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, block, peer.Object.Connection.Id);

            Assert.False(builder.TestConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(additionalHeaders.HashBlock));
            Assert.False(builder.TestConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(additionalHeaders.Previous.HashBlock));

            builder.AssertExpectedBlockSizesEmpty();

            Assert.True(callback1Called);
            Assert.True(callback2Called);
            Assert.True(callback3Called);

            Assert.NotNull(calledWith1);
            Assert.Equal(additionalHeaders, calledWith1.ChainedHeader);

            Assert.NotNull(calledWith2);
            Assert.Equal(additionalHeaders, calledWith2.ChainedHeader);

            Assert.NotNull(calledWith3);
            Assert.Equal(additionalHeaders.Previous, calledWith3.ChainedHeader);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockNotInCT_ReturnsNull()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);

            var result = builder.TestConsensusManager.ConsensusManager.GetBlockDataAsync(new uint256(234)).GetAwaiter().GetResult();

            Assert.Null(result);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockInCT_HasBlock_ReturnsBlock()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var result = builder.TestConsensusManager.ConsensusManager.GetBlockDataAsync(builder.InitialChainTip.HashBlock).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.IsType<ChainedHeaderBlock>(result);
            Assert.Equal(builder.InitialChainTip.Block, result.Block);
            Assert.Equal(builder.InitialChainTip, result.ChainedHeader);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockInCT_HasNoBlock_BlockInBlockStore_ReturnsBlockFromBlockStore()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);

            var initialChainTipBlock = builder.InitialChainTip.Block;
            builder.InitialChainTip.Block = null;
            builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            builder.BlockStore.Setup(g => g.GetBlockAsync(builder.InitialChainTip.HashBlock))
             .ReturnsAsync(() =>
             {
                 return initialChainTipBlock;
             });

            var result = builder.TestConsensusManager.ConsensusManager.GetBlockDataAsync(builder.InitialChainTip.HashBlock).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.IsType<ChainedHeaderBlock>(result);
            Assert.Equal(initialChainTipBlock, result.Block);
            Assert.Equal(builder.InitialChainTip, result.ChainedHeader);
        }

        [Fact]
        public void GetBlockDataAsync_ChainedHeaderBlockInCT_HasNoBlock_BlockNotInBlockStore_ReturnsChainedHeaderBlockFromCT()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);

            builder.InitialChainTip.Block = null;
            builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            builder.BlockStore.Setup(g => g.GetBlockAsync(builder.InitialChainTip.HashBlock))
                .ReturnsAsync(() =>
                {
                    return null;
                });

            var result = builder.TestConsensusManager.ConsensusManager.GetBlockDataAsync(builder.InitialChainTip.HashBlock).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.IsType<ChainedHeaderBlock>(result);
            Assert.Null(result.Block);
            Assert.Equal(builder.InitialChainTip, result.ChainedHeader);
        }


        [Fact]
        public void GetOrDownloadBlocksAsync_ChainedHeaderBlockNotInCT_CallsBlockDownloadedCallbackForBlock_BlocksNotDownloaded()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);

            var callbackCalled = false;
            ChainedHeaderBlock calledWith = null;
            var blockDownloadedCallback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; calledWith = d; });

            var blockHashes = new List<uint256>()
             {
                builder.InitialChainTip.HashBlock
             };
            builder.TestConsensusManager.ConsensusManager.GetOrDownloadBlocksAsync(blockHashes, blockDownloadedCallback).GetAwaiter().GetResult();

            Assert.True(callbackCalled);
            Assert.Null(calledWith);
            builder.BlockPuller.Verify(b => b.RequestBlocksDownload(It.IsAny<List<ChainedHeader>>(), It.IsAny<bool>()), Times.Exactly(0));
        }

        [Fact]
        public void GetOrDownloadBlocksAsync_ChainedHeaderBlockInCTWithoutBlock_DoesNotCallBlockDownloadedCallbackForBlock_BlockDownloaded()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false);
            var blockHashes = new List<uint256>()
            {
                builder.InitialChainTip.HashBlock
            };

            builder.InitialChainTip.Block = null;
            builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();

            var callbackCalled = false;
            ChainedHeaderBlock calledWith = null;
            var blockDownloadedCallback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; calledWith = d; });

            builder.TestConsensusManager.ConsensusManager.GetOrDownloadBlocksAsync(blockHashes, blockDownloadedCallback).GetAwaiter().GetResult();

            Assert.True(builder.TestConsensusManager.CallbacksByBlocksRequestedHashContainsKeyForHash(builder.InitialChainTip.HashBlock));
            Assert.False(callbackCalled);
            builder.BlockPuller.Verify(b => b.RequestBlocksDownload(It.IsAny<List<ChainedHeader>>(), It.IsAny<bool>()), Times.Exactly(1));
        }

        [Fact]
        public void GetOrDownloadBlocksAsync_ChainedHeaderBlockInCTWithBlock_CallsBlockDownloadedCallbackForBlock_BlockNotDownloaded()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var callbackCalled = false;
            ChainedHeaderBlock calledWith = null;
            var blockDownloadedCallback = new Bitcoin.Consensus.OnBlockDownloadedCallback(d => { callbackCalled = true; calledWith = d; });

            var blockHashes = new List<uint256>()
             {
                builder.InitialChainTip.HashBlock
             };

            builder.TestConsensusManager.ConsensusManager.GetOrDownloadBlocksAsync(blockHashes, blockDownloadedCallback).GetAwaiter().GetResult();

            Assert.True(callbackCalled);
            Assert.NotNull(calledWith);
            Assert.IsType<ChainedHeaderBlock>(calledWith);
            Assert.Equal(builder.InitialChainTip.Block, calledWith.Block);
            Assert.Equal(builder.InitialChainTip, calledWith.ChainedHeader);
            builder.BlockPuller.Verify(b => b.RequestBlocksDownload(It.IsAny<List<ChainedHeader>>(), It.IsAny<bool>()), Times.Exactly(0));
        }

        [Fact]
        public void BlockMinedAsync_InvalidPreviousTip_ReturnsNull()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(2, builder.InitialChainTip);

            var result = builder.TestConsensusManager.ConsensusManager.BlockMinedAsync(additionalHeaders.Block).GetAwaiter().GetResult();

            Assert.Null(result);
        }


        [Fact]
        public void BlockMinedAsync_CorrectPreviousTip_PartialValidationError_ThrowsConsensusException()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);

            builder.PartialValidator.Setup(p => p.ValidateAsync(It.Is<ChainedHeader>(c => c.Block == additionalHeaders.Block), additionalHeaders.Block))
                .ReturnsAsync(new Bitcoin.Consensus.ValidationContext()
                {
                    Error = Bitcoin.Consensus.ConsensusErrors.BadBlockSignature
                });

            Assert.Throws<Bitcoin.Consensus.ConsensusException>(() => builder.TestConsensusManager.ConsensusManager.BlockMinedAsync(additionalHeaders.Block).GetAwaiter().GetResult());
            Assert.Equal(builder.InitialChainTip, builder.TestConsensusManager.ConsensusManager.Tip);
        }

        [Fact]
        public void BlockMinedAsync_CorrectPreviousTip_NoPartialValidationError_FullValidationNotRequired_ThrowsConsensusException()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            builder.InitialChainTip.BlockValidationState = ValidationState.HeaderValidated;
            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip, validationState: ValidationState.FullyValidated);

            builder.PartialValidator.Setup(p => p.ValidateAsync(It.Is<ChainedHeader>(c => c.Block == additionalHeaders.Block), additionalHeaders.Block))
                .ReturnsAsync(new Bitcoin.Consensus.ValidationContext()
                {
                    ChainedHeaderToValidate = additionalHeaders
                });

            Assert.Throws<Bitcoin.Consensus.ConsensusException>(() => builder.TestConsensusManager.ConsensusManager.BlockMinedAsync(additionalHeaders.Block).GetAwaiter().GetResult());
            Assert.Equal(builder.InitialChainTip, builder.TestConsensusManager.ConsensusManager.Tip);
        }

        [Fact]
        public void BlockMinedAsync_CorrectPreviousTip_NoPartialValidationError_FullValidationRequired_PassesValidation_ReturnsChainedHeaderToValidate()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);

            builder.PartialValidator.Setup(p => p.ValidateAsync(It.Is<ChainedHeader>(c => c.Block == additionalHeaders.Block), additionalHeaders.Block))
                .ReturnsAsync(new Bitcoin.Consensus.ValidationContext()
                {
                    ChainedHeaderToValidate = additionalHeaders
                });

            builder.FullValidator.Setup(p => p.ValidateAsync(It.Is<ChainedHeader>(c => c.Block == additionalHeaders.Block), additionalHeaders.Block))
                .ReturnsAsync(new Bitcoin.Consensus.ValidationContext()
                {
                    ChainedHeaderToValidate = additionalHeaders
                });

            var result = builder.TestConsensusManager.ConsensusManager.BlockMinedAsync(additionalHeaders.Block).GetAwaiter().GetResult();

            Assert.Equal(additionalHeaders, result);
            Assert.Equal(additionalHeaders, builder.TestConsensusManager.ConsensusManager.Tip);
        }

        [Fact]
        public void BlockMinedAsync_CorrectPreviousTip_NoPartialValidationError_FullValidationRequired_DoesNotPassFullValidation_ThrowsConsensusException()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);

            builder.PartialValidator.Setup(p => p.ValidateAsync(It.Is<ChainedHeader>(c => c.Block == additionalHeaders.Block), additionalHeaders.Block))
                .ReturnsAsync(new Bitcoin.Consensus.ValidationContext()
                {
                    ChainedHeaderToValidate = additionalHeaders
                });
            builder.FullValidator.Setup(p => p.ValidateAsync(It.Is<ChainedHeader>(c => c.Block == additionalHeaders.Block), additionalHeaders.Block))
                .ReturnsAsync(new Bitcoin.Consensus.ValidationContext()
                {
                    Error = Bitcoin.Consensus.ConsensusErrors.BadBlockSignature
                });

            Assert.Throws<Bitcoin.Consensus.ConsensusException>(() => builder.TestConsensusManager.ConsensusManager.BlockMinedAsync(additionalHeaders.Block).GetAwaiter().GetResult());
            Assert.Equal(builder.InitialChainTip, builder.TestConsensusManager.ConsensusManager.Tip);
        }


        [Fact]
        public void OnPartialValidationCompletedCallbackAsync_PartialValidationFails_BansPeer()
        {
            TestContext builder = GetBuildTestContext(10, useCheckpoints: false, initializeWithChainTip: true);

            builder.InitialChainTip.BlockValidationState = ValidationState.PartiallyValidated;
            var additionalHeaders = builder.ExtendAChain(1, builder.InitialChainTip);
            var headerTree = builder.ChainedHeaderToList(additionalHeaders, 1);
            var peer = builder.GetNetworkPeerWithConnection();

            var result = builder.TestConsensusManager.ConsensusManager.HeadersPresented(peer.Object, headerTree, true);

            builder.PartialValidator.Setup(p => p.StartPartialValidation(It.IsAny<ChainedHeader>(), It.IsAny<Block>(), It.IsAny<OnPartialValidationCompletedAsyncCallback>()))
                .Callback<ChainedHeader, Block, OnPartialValidationCompletedAsyncCallback>((header, block, callback) =>
                {
                    callback(new Bitcoin.Consensus.ValidationContext()
                    {
                        BanDurationSeconds = 3000,
                        BlockToValidate = block,
                        ChainedHeaderToValidate = header,
                        Error = Bitcoin.Consensus.ConsensusErrors.BadTransactionScriptError
                    });

                });

            builder.blockPullerBlockDownloadCallback(additionalHeaders.HashBlock, additionalHeaders.Block, peer.Object.Connection.Id);

            builder.AssertPeerBanned(peer.Object);
        }


        private static void AssertBlockSizes(Dictionary<uint256, long> blockSize, List<uint256> expectedBlocks, int expectedSize)
        {
            foreach (var hash in expectedBlocks)
            {
                // checks it exists and is expectedsize at the same time.
                Assert.Equal(expectedSize, blockSize[hash]);
            }
        }

        private static TestContext GetBuildTestContext(int initialChainAmount, bool? useCheckpoints = false, bool initializeWithChainTip = false)
        {
            var contextBuilder = new TestContextBuilder().WithInitialChain(initialChainAmount);

            if (useCheckpoints.HasValue)
            {
                contextBuilder = contextBuilder.UseCheckpoints(useCheckpoints.Value);
            }

            var builder = contextBuilder.Build();


            if (initializeWithChainTip)
            {
                builder.TestConsensusManager.ConsensusManager.InitializeAsync(builder.InitialChainTip).GetAwaiter().GetResult();
            }

            return builder;
        }
    }
}