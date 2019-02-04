using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class RewindDataIndexCacheTest : LogsTestBase
    {
        public RewindDataIndexCacheTest() : base(new StratisTest())
        {
            // override max reorg to 10
            Type consensusType = typeof(NBitcoin.Consensus);
            consensusType.GetProperty("MaxReorgLength").SetValue(this.Network.Consensus, (uint)10);

        }

        [Fact]
        public async Task RewindDataIndex_InitialiseCache_BelowMaxREprgAsync()
        {
            Mock<IDateTimeProvider> dateTimeProviderMock = new Mock<IDateTimeProvider>();
            Mock<ICoinView> coinViewMock = new Mock<ICoinView>();
            this.SetupMockCoinView(coinViewMock);

            RewindDataIndexCache rewindDataIndexCache = new RewindDataIndexCache(dateTimeProviderMock.Object, this.Network);

            await rewindDataIndexCache.InitializeAsync(5, coinViewMock.Object);

            var items = rewindDataIndexCache.GetMemberValue("items") as ConcurrentDictionary<string, int>;

            items.Should().HaveCount(10);
            this.CheckCache(items, 5, 1);
        }

        [Fact]
        public async Task RewindDataIndex_InitialiseCacheAsync()
        {
            Mock<IDateTimeProvider> dateTimeProviderMock = new Mock<IDateTimeProvider>();
            Mock<ICoinView> coinViewMock = new Mock<ICoinView>();
            this.SetupMockCoinView(coinViewMock);

            RewindDataIndexCache rewindDataIndexCache = new RewindDataIndexCache(dateTimeProviderMock.Object, this.Network);

            await rewindDataIndexCache.InitializeAsync(20, coinViewMock.Object);

            var items = rewindDataIndexCache.GetMemberValue("items") as ConcurrentDictionary<string, int>;

            items.Should().HaveCount(22);
            this.CheckCache(items, 20, 10);
        }

        [Fact]
        public async Task RewindDataIndex_SaveAsync()
        {
            Mock<IDateTimeProvider> dateTimeProviderMock = new Mock<IDateTimeProvider>();
            Mock<ICoinView> coinViewMock = new Mock<ICoinView>();
            this.SetupMockCoinView(coinViewMock);

            RewindDataIndexCache rewindDataIndexCache = new RewindDataIndexCache(dateTimeProviderMock.Object, this.Network);

            await rewindDataIndexCache.InitializeAsync(20, coinViewMock.Object);

            rewindDataIndexCache.Save(new Dictionary<string, int>() { { $"{ new uint256(21) }-{ 0 }", 21}});
            var items = rewindDataIndexCache.GetMemberValue("items") as ConcurrentDictionary<string, int>;

            items.Should().HaveCount(23);
            this.CheckCache(items, 21, 10);
        }

        [Fact]
        public async Task RewindDataIndex_FlushAsync()
        {
            Mock<IDateTimeProvider> dateTimeProviderMock = new Mock<IDateTimeProvider>();
            Mock<ICoinView> coinViewMock = new Mock<ICoinView>();
            this.SetupMockCoinView(coinViewMock);

            RewindDataIndexCache rewindDataIndexCache = new RewindDataIndexCache(dateTimeProviderMock.Object, this.Network);

            await rewindDataIndexCache.InitializeAsync(20, coinViewMock.Object);

            rewindDataIndexCache.Flush(15);
            var items = rewindDataIndexCache.GetMemberValue("items") as ConcurrentDictionary<string, int>;

            items.Should().HaveCount(12);
            this.CheckCache(items, 15, 9);
        }

        [Fact]
        public async Task RewindDataIndex_RemoveAsync()
        {
            Mock<IDateTimeProvider> dateTimeProviderMock = new Mock<IDateTimeProvider>();
            Mock<ICoinView> coinViewMock = new Mock<ICoinView>();
            this.SetupMockCoinView(coinViewMock);

            RewindDataIndexCache rewindDataIndexCache = new RewindDataIndexCache(dateTimeProviderMock.Object, this.Network);

            await rewindDataIndexCache.InitializeAsync(20, coinViewMock.Object);

            await rewindDataIndexCache.Remove(19, coinViewMock.Object);
            var items = rewindDataIndexCache.GetMemberValue("items") as ConcurrentDictionary<string, int>;

            items.Should().HaveCount(22);
            this.CheckCache(items, 19, 9);
        }


        private void CheckCache(ConcurrentDictionary<string, int> items, int tip, int bottom)
        {
            foreach (KeyValuePair<string, int> keyValuePair in items)
            {
                Assert.True(keyValuePair.Value <= tip && keyValuePair.Value >= bottom);
            }
        }

        private void SetupMockCoinView(Mock<ICoinView> coinViewMock)
        {
            // set up coinview with 2 blocks and 2 utxo per block.
            ulong index = 1;
            coinViewMock.Setup(c => c.GetRewindData(It.IsAny<int>())).Returns(() => Task.FromResult(new RewindData()
            {
                OutputsToRestore = new List<UnspentOutputs>() { new UnspentOutputs(new uint256(index++), new Coins()) { Outputs = new TxOut[] { new TxOut(), new TxOut() } } }
            }));
        }
    }
}
