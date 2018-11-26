﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderStoreTests : LogsTestBase
    {
        private readonly ProvenBlockHeaderStore provenBlockHeaderStore;
        private readonly IProvenBlockHeaderRepository provenBlockHeaderRepository;

        public ProvenBlockHeaderStoreTests() : base(KnownNetworks.StratisTest)
        {
            var nodeStats = new NodeStats(DateTimeProvider.Default);

            this.provenBlockHeaderRepository = new ProvenBlockHeaderRepository(this.Network, CreateTestDir(this), this.LoggerFactory.Object);

            this.provenBlockHeaderStore = new ProvenBlockHeaderStore(DateTimeProvider.Default, this.LoggerFactory.Object, this.provenBlockHeaderRepository, nodeStats);
        }

        [Fact]
        public async Task InitialiseStoreToGenesisChainHeaderAsync()
        {
            var genesis = this.BuildChainWithProvenHeaders(1);

            await this.provenBlockHeaderStore.InitializeAsync(genesis).ConfigureAwait(false);

            this.provenBlockHeaderStore.TipHashHeight.Hash.Should().Be(genesis.HashBlock);
        }

        [Fact]
        public async Task GetAsync_Get_Items_From_StoreAsync()
        {
            var tip = this.BuildChainWithProvenHeaders(3);

            SortedDictionary<int, ProvenBlockHeader> headers = this.ConvertToDictionaryOfProvenHeaders(tip);
            await this.provenBlockHeaderRepository.PutAsync(headers, new HashHeightPair(tip.HashBlock, tip.Height)).ConfigureAwait(false);

            // Load saved headers.
            using (IProvenBlockHeaderStore store = this.SetupStore())
            {
                var outHeaders = await store.GetAsync(0, tip.Height).ConfigureAwait(false);

                Assert.Equal(tip.Height, outHeaders.Count);
            }
        }


        [Fact]
        public async Task AddToPending_Adds_To_CacheAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync(this.BuildChainWithProvenHeaders(1)).ConfigureAwait(false);

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
            await this.provenBlockHeaderStore.InitializeAsync(BuildChainWithProvenHeaders(1)).ConfigureAwait(false);

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

            WaitLoop(() =>
            {
                // Check if it has been saved to disk.
                var outHeaderRepo = this.provenBlockHeaderRepository.GetAsync(0).GetAwaiter().GetResult();
                if (outHeaderRepo == null)
                    return false;
                return outHeaderRepo.GetHash() == outHeader.GetHash();
            });
        }

        [Fact]
        public async Task Add_2k_ProvenHeaders_ToPending_CacheAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync(BuildChainWithProvenHeaders(1)).ConfigureAwait(false);

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
            await this.provenBlockHeaderStore.InitializeAsync(BuildChainWithProvenHeaders(1)).ConfigureAwait(false);

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
            WaitLoop(() =>
            {
                var pendingTipHashHeight = this.provenBlockHeaderStore.GetMemberValue("pendingTipHashHeight");
                return pendingTipHashHeight == null;
            });

            WaitLoop(() =>
            {
                // Check if it has been saved to disk.
                var outHeaderRepo = this.provenBlockHeaderRepository.GetAsync(1999).ConfigureAwait(false).GetAwaiter().GetResult();
                return outHeaderRepo != null;
            });

            // Check items in cache - should now be empty.
            cacheCount = this.provenBlockHeaderStore.PendingBatch.GetMemberValue("Count");
            cacheCount.Should().Be(0);
        }

        [Fact]
        public async Task GetAsync_Add_Items_Greater_Than_Max_Size_Cache_Stays_Below_Max_SizeAsync()
        {
            // Initialise store.
            await this.provenBlockHeaderStore.InitializeAsync(BuildChainWithProvenHeaders(1)).ConfigureAwait(false);

            var inHeaders = new SortedDictionary<int, ProvenBlockHeader>();

            // Add maximum cache count items headers.
            for (int i = 0; i < 10; i++)
                inHeaders.Add(i, CreateNewProvenBlockHeaderMock());

            long maxSize = 500;

            // Reduce the MaxMemoryCacheSizeInBytes size for test.
            FieldInfo field = typeof(MemorySizeCache<int, ProvenBlockHeader>).GetField("maxSize", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(this.provenBlockHeaderStore.Cache, maxSize);

            // Add items to the repository.
            await this.provenBlockHeaderRepository.PutAsync(inHeaders,
                new HashHeightPair(inHeaders.LastOrDefault().Value.GetHash(), inHeaders.Count - 1)).ConfigureAwait(false);

            // Asking for headers will check the cache store.  If the header is not in the cache store, then it will check the repository and add the cache store.
            var outHeaders = await this.provenBlockHeaderStore.GetAsync(0, inHeaders.Count - 1).ConfigureAwait(false);

            this.provenBlockHeaderStore.Cache.TotalSize.Should().BeLessThan(maxSize);
        }

        [Fact]
        public async Task GetAsync_Headers_Should_Be_In_Consecutive_OrderAsync()
        {
            var inItems = new List<ProvenBlockHeader>();

            ProvenBlockHeader provenHeaderMock;

            await this.provenBlockHeaderStore.InitializeAsync(this.BuildChainWithProvenHeaders(1)).ConfigureAwait(false);
            uint nonceIndex = 1; // a random index to change the header hash.

            // Save items 0 - 9 to disk.
            for (int i = 0; i < 10; i++)
            {
                provenHeaderMock = CreateNewProvenBlockHeaderMock();
                provenHeaderMock.Nonce = ++nonceIndex;
                this.provenBlockHeaderStore.AddToPendingBatch(provenHeaderMock, new HashHeightPair(provenHeaderMock.GetHash(), i));
                inItems.Add(provenHeaderMock);
            }

            // Save to disk and cache is cleared.
            this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

            // When pendingTipHashHeight is null we can safely say the items were saved to the repository, based on the above SaveAsync.
            WaitLoop(() =>
            {
                var tipHashHeight = this.provenBlockHeaderStore.GetMemberValue("TipHashHeight") as HashHeightPair;
                return tipHashHeight.Height == 9;
            });

            // Clear cache.
            for (int i = 0; i < 10; i++)
            {
                this.provenBlockHeaderStore.Cache.Remove(i);
            }

            // Add item 4 to cache
            var provenHeaderMock1 = CreateNewProvenBlockHeaderMock();
            provenHeaderMock1.Nonce = ++nonceIndex;
            this.provenBlockHeaderStore.AddToPendingBatch(provenHeaderMock1, new HashHeightPair(provenHeaderMock1.GetHash(), 4));
            inItems[4] = provenHeaderMock1;

            // Add item 6 to cache.
            var provenHeaderMock2 = CreateNewProvenBlockHeaderMock();
            provenHeaderMock2.Nonce = ++nonceIndex;
            this.provenBlockHeaderStore.AddToPendingBatch(provenHeaderMock2, new HashHeightPair(provenHeaderMock2.GetHash(), 6));
            inItems[6] = provenHeaderMock2;

            // Load the items and make sure in sequence.
            var outItems = await this.provenBlockHeaderStore.GetAsync(0, 9).ConfigureAwait(false);

            outItems.Count.Should().Be(10);

            // Items 4 and 6 were added to pending cache and have the same block hash.
            for (int i = 0; i < 10; i++)
            {
                outItems[i].GetHash().Should().Be(inItems[i].GetHash());
            }
        }

        // Commented out because those tests test incorrect logic in PH store.
        /*
        [Fact]
        public async Task InitializeAsync_When_Chain_Tip_Reverts_Back_To_Genesis_Store_Tip_Is_In_SyncAsync()
        {
            var chainWithHeaders = BuildChainWithProvenHeaders(3);

            Dictionary<int, ProvenBlockHeader> provenBlockheaders = this.ConvertToDictionaryOfProvenHeaders(chainWithHeaders);

            await this.provenBlockHeaderRepository.PutAsync(
                provenBlockheaders,
                new HashHeightPair(provenBlockheaders.Last().Value.GetHash(), provenBlockheaders.Count - 1)).ConfigureAwait(false);

            using (IProvenBlockHeaderStore store = this.SetupStore())
            {
                // Revert back to Genesis.
                await store.InitializeAsync(chainWithHeaders.Previous.Previous);

                store.TipHashHeight.Hash.Should().Be(chainWithHeaders.Previous.Previous.HashBlock);
            }
        }

        [Fact]
        public async Task InitializeAsync_When_Tip_Reorg_Occurs_Behind_Current_TipAsync()
        {
            // Chain - 1 - 2 - 3 - 4 - 5 (tip at 5).
            var chainWithHeaders = BuildChainWithProvenHeaders(5);
            Dictionary<int, ProvenBlockHeader> provenBlockheaders = this.ConvertToDictionaryOfProvenHeaders(chainWithHeaders);

            // Persist current chain.
            await this.provenBlockHeaderRepository.PutAsync(
                provenBlockheaders,
                new HashHeightPair(provenBlockheaders.Last().Value.GetHash(), provenBlockheaders.Count - 1)).ConfigureAwait(false);

            using (IProvenBlockHeaderStore store = this.SetupStore())
            {
                // Reorganised chain - 1 - 2 - 3  (tip at 3).
                await store.InitializeAsync(chainWithHeaders.Previous.Previous).ConfigureAwait(true);

                store.TipHashHeight.Hash.Should().Be(chainWithHeaders.Previous.Previous.Header.GetHash());
            }
        }

        [Fact]
        public void InitializeAsync_When_Behind_Reorg_Occurred_SetTipTo_New_ChainHeader()
        {
            // Chain - Chain - 1 - 2 - 3
            var chainWithHeaders = BuildChainWithProvenHeaders(3);
            Dictionary<int, ProvenBlockHeader> provenBlockheaders = this.ConvertToDictionaryOfProvenHeaders(chainWithHeaders);

            var provenBlockHeadersToPush = provenBlockheaders.ToDictionary(a => a.Key, b => b.Value);
            var hashHeightPair = new HashHeightPair(provenBlockheaders.Last().Value.GetHash(), provenBlockheaders.Count - 1);
            this.provenBlockHeaderRepository.PutAsync(provenBlockHeadersToPush, hashHeightPair).GetAwaiter().GetResult();

            // Create a new chain which a different hash block.
            chainWithHeaders = BuildChainWithProvenHeaders(2);

            using (IProvenBlockHeaderStore store = this.SetupStore())
            {
                store.InitializeAsync(chainWithHeaders).GetAwaiter().GetResult();

                store.TipHashHeight.Hash.Should().Be(chainWithHeaders.HashBlock);
                store.TipHashHeight.Height.Should().Be(chainWithHeaders.Height);
            }
        }

        [Fact]
        public async Task InitializeAsync_When_Behind_Reorg_Occurs_Throws_Exception_When_New_ChainHeader_Tip_Is_HigherAsync()
        {
            // Chain - Chain - 1 - 2 - 3
            var chainWithHeaders = BuildChainWithProvenHeaders(3);
            Dictionary<int, ProvenBlockHeader> provenBlockheaders = this.ConvertToDictionaryOfProvenHeaders(chainWithHeaders);

            var provenBlockHeadersToPush = provenBlockheaders.ToDictionary(a => a.Key, b => b.Value);
            var hashHeightPair = new HashHeightPair(provenBlockheaders.Last().Value.GetHash(), provenBlockheaders.Count - 1);
            await this.provenBlockHeaderRepository.PutAsync(provenBlockHeadersToPush, hashHeightPair).ConfigureAwait(false);

            // Create a new chain which a different hash block.
            chainWithHeaders = BuildChainWithProvenHeaders(4);

            using (IProvenBlockHeaderStore store = this.SetupStore())
            {
                Func<Task> act = async () => { await store.InitializeAsync(chainWithHeaders).ConfigureAwait(false); };

                act.Should().Throw<ProvenBlockHeaderException>().WithMessage("Chain header tip hash does not match the latest proven block header hash saved to disk.");
            }
        }
        */

        [Fact]
        public void AddToPending_Then_Save_Incorrect_Sequence_Push_To_Store()
        {
            var inHeader = CreateNewProvenBlockHeaderMock();

            // Add headers to pending batch in the wrong height order.
            for (int i = 1; i >= 0; i--)
            {
                this.provenBlockHeaderStore.AddToPendingBatch(inHeader, new HashHeightPair(inHeader.GetHash(), i));
            }

            var taskResult = this.provenBlockHeaderStore.InvokeMethod("SaveAsync") as Task;
            taskResult.Wait();

            taskResult.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Fact]
        public async Task AddToPending_Store_TipHash_Is_The_Same_As_ChainHeaderTipAsync()
        {
            var chainWithHeaders = BuildChainWithProvenHeaders(3);
            SortedDictionary<int, ProvenBlockHeader> provenBlockheaders = this.ConvertToDictionaryOfProvenHeaders(chainWithHeaders);

            // Persist current chain.
            await this.provenBlockHeaderRepository.PutAsync(
                provenBlockheaders,
                new HashHeightPair(provenBlockheaders.Last().Value.GetHash(), provenBlockheaders.Count - 1)).ConfigureAwait(false);

            using (IProvenBlockHeaderStore store = this.SetupStore())
            {
                var header = CreateNewProvenBlockHeaderMock();

                this.provenBlockHeaderStore.AddToPendingBatch(header, new HashHeightPair(header.GetHash(), chainWithHeaders.Height));

                this.provenBlockHeaderStore.InvokeMethod("SaveAsync");

                HashHeightPair tipHashHeight = null;

                WaitLoop(() =>
                {
                    tipHashHeight = this.provenBlockHeaderStore.GetMemberValue("TipHashHeight") as HashHeightPair;
                    return tipHashHeight == this.provenBlockHeaderRepository.TipHashHeight;
                });

                tipHashHeight.Height.Should().Be(chainWithHeaders.Height);
            }
        }

        private ProvenBlockHeaderStore SetupStore()
        {
            return new ProvenBlockHeaderStore(DateTimeProvider.Default, this.LoggerFactory.Object, this.provenBlockHeaderRepository, new NodeStats(DateTimeProvider.Default));
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
