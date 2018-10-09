using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreTests : LogsTestBase
    {
        private readonly Network network = KnownNetworks.StratisTest;
        private readonly Mock<IConsensusManager> consensusManager;
        private ConcurrentChain concurrentChain;
        private readonly ProvenBlockHeaderStore provenBlockHeaderStore;
        private IProvenBlockHeaderRepository provenBlockHeaderRepository;
        private readonly Mock<INodeLifetime> nodeLifetime;
        private readonly Mock<IAsyncLoopFactory> asyncLoopFactoryLoop;
        private readonly string Folder;
        private readonly NodeStats nodeStats;

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            this.consensusManager = new Mock<IConsensusManager>();
            this.concurrentChain = this.GenerateChainWithHeight(3);
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.nodeStats = new NodeStats(DateTimeProvider.Default);
            this.asyncLoopFactoryLoop = new Mock<IAsyncLoopFactory>();

            this.Folder = CreateTestDir(this);

            this.provenBlockHeaderRepository = new ProvenBlockHeaderRepository(this.network, this.Folder, this.LoggerFactory.Object);

            this.provenBlockHeaderStore = new ProvenBlockHeaderStore(
                this.concurrentChain, DateTimeProvider.Default, this.LoggerFactory.Object,
                this.provenBlockHeaderRepository, this.nodeLifetime.Object, this.nodeStats, this.asyncLoopFactoryLoop.Object);
        }

        [Fact]
        public async Task InitialiseStoreToGenesisChainHeaderAsync()
        {
            ChainedHeader chainedHeader = this.concurrentChain.Genesis;

            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            this.provenBlockHeaderStore.TipHashHeight.Hash.Should().Be(this.network.GetGenesis().GetHash());
        }

        [Fact]
        public async Task LoadItemsAsync()
        {
            // Put 3 items to in the repository - items created in the constructor.
            var inItems = new List<ProvenBlockHeader>();

            var provenHeaderMock = CreateNewProvenBlockHeaderMock();

            ChainedHeader chainedHeader = this.concurrentChain.Tip;

            while(chainedHeader != null)
            {
                inItems.Add(provenHeaderMock);

                chainedHeader = chainedHeader.Previous;
            }

            await this.provenBlockHeaderRepository.PutAsync(inItems, new HashHeightPair(provenHeaderMock.GetHash(), inItems.Count - 1)).ConfigureAwait(false);

            // Then load them.
            using (IProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                var outItems = await store.GetAsync(0, inItems.Count).ConfigureAwait(false);

                outItems.Count.Should().Be(inItems.Count);

                foreach(var item in outItems)
                {
                    item.GetHash().Should().Be(provenHeaderMock.GetHash());
                }
            }
        }

        [Fact]
        public async Task AddToPending_Adds_To_CacheAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            // Add to pending (add to internal cache).
            var inHeader = CreateNewProvenBlockHeaderMock();
            this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), 1));

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingBatch.GetMemberValue("Count");
            cacheCount.Should().Be(1);

            // Get item.
            var outHeader = await this.provenBlockHeaderStore.GetAsync(1).ConfigureAwait(false);
            outHeader.GetHash().Should().Be(inHeader.GetHash());

            // Check if it has been saved to disk.  It shouldn't as the asyncLoopFactory() would not have been called yet.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(1, 1).ConfigureAwait(false);
            outHeaderRepo.FirstOrDefault().Should().BeNull();
        }

        [Fact]
        public async Task AddToPending_Adds_To_Cache_Then_Save_To_DiskAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            // Add to pending (add to internal cache).
            var inHeader = CreateNewProvenBlockHeaderMock();
            this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), 0));

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingBatch.GetMemberValue("Count");
            cacheCount.Should().Be(1);

            // Get item.
            var outHeader = await this.provenBlockHeaderStore.GetAsync(0).ConfigureAwait(false);
            outHeader.GetHash().Should().Be(inHeader.GetHash());

            // Call the internal save method to save cached item to disk.
            this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

            // when pendingTipHashHeight is null we can safely say the items were saved to the repository, based on the above SaveAsync.
            WaitLoop(() => {
                var pendingTipHashHeight = this.provenBlockHeaderStore.GetMemberValue("pendingTipHashHeight");
                return pendingTipHashHeight == null;
            });

            // Check if it has been saved to disk.  It shouldn't as the asyncLoopFactory() would not have been called yet.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(0).ConfigureAwait(false);
            outHeaderRepo.GetHash().Should().Be(outHeader.GetHash());
        }

        [Fact]
        public async Task Add_2k_ProvenHeaders_ToPending_CacheAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            ProvenBlockHeader inHeader = null;

            // Add to pending (add to internal cache).
            for (int i = 0; i < 2_000; i++)
            {
                inHeader = CreateNewProvenBlockHeaderMock();
                this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), i));
            }

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingBatch.GetMemberValue("Count");
            cacheCount.Should().Be(2_000);

            // Check if it has been saved to disk.  It shouldn't as the asyncLoopFactory() would not have been called yet.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(1, 1).ConfigureAwait(false);
            outHeaderRepo.FirstOrDefault().Should().BeNull();
        }

        [Fact]
        public async Task Add_2k_ProvenHeaders_To_PendingBatch_Then_Save_Then_PendingBatch_Should_Be_EmptyAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            ProvenBlockHeader inHeader = null;

            // Add to pending (add to internal cache).
            for (int i = 0; i < 2_000; i++)
            {
                inHeader = CreateNewProvenBlockHeaderMock();
                this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), i));
            }

            // Check Item in cache.
            var cacheCount = this.provenBlockHeaderStore.PendingBatch.GetMemberValue("Count");
            cacheCount.Should().Be(2_000);

            // Call the internal save method to save cached item to disk.
            this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

            // when pendingTipHashHeight is null we can safely say the items were saved to the repository, based on the above SaveAsync.
            WaitLoop(() => {
                var pendingTipHashHeight = this.provenBlockHeaderStore.GetMemberValue("pendingTipHashHeight");
                return pendingTipHashHeight == null;
            });

            // Check if it has been saved to disk.
            var outHeaderRepo = await this.provenBlockHeaderRepository.GetAsync(1999).ConfigureAwait(false);
            outHeaderRepo.Should().NotBeNull();

            // Check items in cache - should now be empty.
            cacheCount = this.provenBlockHeaderStore.PendingBatch.GetMemberValue("Count");
            cacheCount.Should().Be(0);
        }

        [Fact]
        public async Task GetAsync_Add_Items_Greater_Than_Max_Size_Cache_Stays_Below_Max_SizeAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync().ConfigureAwait(false);

            var inHeaders = new List<ProvenBlockHeader>();

            // Add maximum cache count items headers.
            for (int i = 0; i < 10; i++)
                inHeaders.Add(CreateNewProvenBlockHeaderMock());

            long maxSize = 500;

            // Reduce the MaxMemoryCacheSizeInBytes size for test.
            FieldInfo field = typeof(MemorySizeCache<int, ProvenBlockHeader>).GetField("maxSize", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(this.provenBlockHeaderStore.Cache, maxSize);

            // Add items to the repository.
            await this.provenBlockHeaderRepository.PutAsync(inHeaders,
                new HashHeightPair(inHeaders.LastOrDefault().GetHash(), inHeaders.Count - 1)).ConfigureAwait(false);

            // Asking for headers will check the cache store.  If the header is not in the cache store, then it will check the repository and add the cache store.
            var outHeaders = await this.provenBlockHeaderStore.GetAsync(0, inHeaders.Count - 1).ConfigureAwait(false);

            long cacheSizeInBytes = (long)this.provenBlockHeaderStore.Cache.GetMemberValue("totalSize");

            cacheSizeInBytes.Should().BeLessThan(maxSize);
        }

        [Fact]
        public async Task InitializeAsync_When_Tip_Hash_Is_Genesis_Store_Tip_Is_GenesisAsync()
        {
            this.concurrentChain = new ConcurrentChain(this.network);

            var chainWithHeaders = BuildChainWithProvenHeaders(1, this.network);

            this.concurrentChain = chainWithHeaders.concurrentChain;
            var provenBlockheaders = chainWithHeaders.provenBlockHeaders;

            // clear chain to cause the store revert back to genesis.
            this.concurrentChain = new ConcurrentChain(this.network);

            await this.provenBlockHeaderRepository.PutAsync(
                provenBlockheaders,
                new HashHeightPair(provenBlockheaders.Last().GetHash(), provenBlockheaders.Count - 1)).ConfigureAwait(false);

            using (IProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                await store.InitializeAsync();

                store.TipHashHeight.Hash.Should().Be(this.network.GetGenesis().GetHash());
            }
        }

        [Fact]
        public void InitializeAsync_When_Hash_Not_In_Chain_Or_Repo_Throw_Exception()
        {
            this.concurrentChain = new ConcurrentChain(this.network);

            this.provenBlockHeaderRepository.SetPrivatePropertyValue("TipHashHeight", new HashHeightPair(new uint256(), 1));

            using (ProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                new Action(() => store.InitializeAsync().Wait())
                    .Should().Throw<ProvenHeaderStoreException>()
                    .And.Message.Should().Be("Proven block header store failed to recover.");
            }
        }

        [Fact]
        public async Task InitializeAsync_When_Tip_Reorg_Occurs_Tip_Is_Most_RecentAsync()
        {
            this.concurrentChain = new ConcurrentChain(this.network);

            // Chain - 1a | 2a | 3a | 4a | 5a (tip at 5a).
            var chainWithHeaders = BuildChainWithProvenHeaders(5, this.network);

            this.concurrentChain = chainWithHeaders.concurrentChain;
            var provenBlockheaders = chainWithHeaders.provenBlockHeaders;

            // Persist current chain.
            await this.provenBlockHeaderRepository.PutAsync(
                provenBlockheaders,
                new HashHeightPair(provenBlockheaders.Last().GetHash(), provenBlockheaders.Count - 1)).ConfigureAwait(false);

            // Reorg chain - 1b | 2b | 3b (tip now at 3b).
            this.concurrentChain = new ConcurrentChain(this.network);

            var chainWithHeadersReorg = BuildChainWithProvenHeaders(3, this.network);

            this.concurrentChain = chainWithHeadersReorg.concurrentChain;

            using (IProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                await store.InitializeAsync();

                store.TipHashHeight.Hash.Should().Be(chainWithHeadersReorg.concurrentChain.Tip.Header.GetHash());
            }
        }

        [Fact]
        public void AddToPending_Then_Save_Incorrect_Sequence_Thrown_Exception()
        {
            var inHeader = CreateNewProvenBlockHeaderMock();

            // Add headers to pending batch in the wrong height order.
            this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), 1));
            this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), 0));

            var taskResult = this.provenBlockHeaderStore.InvokeMethod("SaveAsync") as Task;

            taskResult.IsFaulted.Should().BeTrue();
            taskResult.Exception.InnerExceptions.Count.Should().Be(1);
            taskResult.Exception.InnerExceptions[0].Should().BeOfType<ProvenHeaderStoreException>();
            taskResult.Exception.InnerExceptions[0].Message.Should().Be("Invalid ProvenBlockHeader pending batch sequence - unable to save to the database repository.");
        }

        [Fact]
        public async Task AddToPending_Store_TipHash_Is_The_Same_As_ChainHeaderTipAsync()
        {
            this.concurrentChain = new ConcurrentChain(this.network);

            var chainWithHeaders = BuildChainWithProvenHeaders(3, this.network);

            this.concurrentChain = chainWithHeaders.concurrentChain;
            var provenBlockheaders = chainWithHeaders.provenBlockHeaders;

            // Persist current chain.
            await this.provenBlockHeaderRepository.PutAsync(
                provenBlockheaders,
                new HashHeightPair(provenBlockheaders.Last().GetHash(), provenBlockheaders.Count - 1)).ConfigureAwait(false);

            using (IProvenBlockHeaderStore store = this.SetupStore(this.Folder))
            {
                var header = CreateNewProvenBlockHeaderMock();

                this.provenBlockHeaderStore.AddToPendingBatch(header, new HashHeightPair(header.GetHash(), this.concurrentChain.Tip.Height));

                this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

                HashHeightPair tipHashHeight = null;

                WaitLoop(() => {
                    tipHashHeight = this.provenBlockHeaderStore.GetMemberValue("TipHashHeight") as HashHeightPair;
                    return tipHashHeight == this.provenBlockHeaderRepository.TipHashHeight;
                });

                tipHashHeight.Height.Should().Be(this.concurrentChain.Tip.Height);
            }
        }

        private ProvenBlockHeaderStore SetupStore(string folder)
        {
            return new ProvenBlockHeaderStore(
                this.concurrentChain, DateTimeProvider.Default,
                this.LoggerFactory.Object, this.provenBlockHeaderRepository,
                this.nodeLifetime.Object, new NodeStats(DateTimeProvider.Default), this.asyncLoopFactoryLoop.Object);
        }

        private ConcurrentChain GenerateChainWithHeight(int blockAmount)
        {
            var chain = new ConcurrentChain(this.network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = this.network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;
                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private static void WaitLoop(Func<bool> act, string failureReason = "Unknown Reason", int retryDelayInMiliseconds = 1000, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken = cancellationToken == default(CancellationToken)
                ? new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 60 * 1000).Token
                : cancellationToken;

            while (!act())
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(retryDelayInMiliseconds);
                }
                catch (OperationCanceledException e)
                {
                    Assert.False(true, $"{failureReason}{Environment.NewLine}{e.Message}");
                }
            }
        }
    }
}
