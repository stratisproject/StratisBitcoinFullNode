using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class CoinViewStackTest
    {
        [Fact]
        public void Constructor_CoinViewWithoutBackedCoinViews_SetsCoinViewAsTopAndBottom()
        {
            var coinView = new NonBackedCoinView();

            var stack = new CoinViewStack(coinView);

            Assert.True(stack.Top is NonBackedCoinView);
            Assert.True(stack.Bottom is NonBackedCoinView);
        }

        [Fact]
        public void Constructor_CoinViewWithBackedCoinViews_SetsTopAndBottom()
        {
            var nonBackedCoinView = new NonBackedCoinView();
            var backedCoinView2 = new BackedCoinView2(nonBackedCoinView);
            var backedCoinView1 = new BackedCoinView1(backedCoinView2);

            var stack = new CoinViewStack(backedCoinView1);

            Assert.True(stack.Top is BackedCoinView1);
            Assert.True(stack.Bottom is NonBackedCoinView);
        }

        [Fact]
        public void GetElements_CoinViewWithBackedCoinViews_ReturnsStack()
        {
            var nonBackedCoinView = new NonBackedCoinView();
            var backedCoinView2 = new BackedCoinView2(nonBackedCoinView);
            var backedCoinView1 = new BackedCoinView1(backedCoinView2);

            var stack = new CoinViewStack(backedCoinView1);

            List<ICoinView> coinViews = stack.GetElements().ToList();

            Assert.Equal(3, coinViews.Count);
            Assert.True(coinViews[0] is BackedCoinView1);
            Assert.True(coinViews[1] is BackedCoinView2);
            Assert.True(coinViews[2] is NonBackedCoinView);
        }

        [Fact]
        public void GetElements_NullCoinViewWithinStack_ReturnsNonNullCoinViews()
        {
            var backedCoinView2 = new BackedCoinView2(null);
            var backedCoinView1 = new BackedCoinView1(backedCoinView2);

            var stack = new CoinViewStack(backedCoinView1);

            List<ICoinView> coinViews = stack.GetElements().ToList();

            Assert.Equal(2, coinViews.Count);
            Assert.True(coinViews[0] is BackedCoinView1);
            Assert.True(coinViews[1] is BackedCoinView2);
        }

        [Fact]
        public void Find_CoinViewTop_ReturnsCoinView()
        {
            var nonBackedCoinView = new NonBackedCoinView();
            var backedCoinView2 = new BackedCoinView2(nonBackedCoinView, 3);
            var backedCoinView1 = new BackedCoinView1(backedCoinView2, 4);

            var stack = new CoinViewStack(backedCoinView1);

            var coinView = stack.Find<BackedCoinView1>();

            Assert.True(coinView is BackedCoinView1);
            Assert.Equal(4, coinView.OutputCount);
        }

        [Fact]
        public void Find_CoinViewWithinStack_ReturnsCoinView()
        {
            var nonBackedCoinView = new NonBackedCoinView();
            var backedCoinView2 = new BackedCoinView2(nonBackedCoinView, 3);
            var backedCoinView1 = new BackedCoinView1(backedCoinView2, 4);

            var stack = new CoinViewStack(backedCoinView1);

            var coinView = stack.Find<BackedCoinView2>();

            Assert.True(coinView is BackedCoinView2);
            Assert.Equal(3, coinView.OutputCount);
        }

        [Fact]
        public void Find_CoinViewNotFound_ReturnsNull()
        {
            var nonBackedCoinView = new NonBackedCoinView();

            var stack = new CoinViewStack(nonBackedCoinView);

            var coinView = stack.Find<BackedCoinView2>();

            Assert.Null(coinView);
        }

        private class NonBackedCoinView : ICoinView
        {
            public NonBackedCoinView()
            {
            }

            /// <inheritdoc />
            public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<uint256> RewindAsync()
            {
                throw new NotImplementedException();
            }

            public Task<RewindData> GetRewindData(int height)
            {
                throw new NotImplementedException();
            }

            public Task SaveChangesAsync(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, int height, List<RewindData> rewindDataList = null)
            {
                throw new NotImplementedException();
            }
        }

        private class BackedCoinView1 : ICoinView, IBackedCoinView
        {
            private BackedCoinView2 inner;

            public BackedCoinView1(BackedCoinView2 inner, int outputCount = 0)
            {
                this.inner = inner;
                this.OutputCount = outputCount;
            }

            public int OutputCount { get; }
            public ICoinView Inner => this.inner;

            /// <inheritdoc />
            public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<uint256> RewindAsync()
            {
                throw new NotImplementedException();
            }

            public Task<RewindData> GetRewindData(int height)
            {
                throw new NotImplementedException();
            }

            public Task SaveChangesAsync(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, int height, List<RewindData> rewindDataList = null)
            {
                throw new NotImplementedException();
            }
        }

        private class BackedCoinView2 : ICoinView, IBackedCoinView
        {
            private NonBackedCoinView inner;

            public BackedCoinView2(NonBackedCoinView inner, int outputCount = 0)
            {
                this.inner = inner;
                this.OutputCount = outputCount;
            }

            public int OutputCount { get; }
            public ICoinView Inner => this.inner;

            /// <inheritdoc />
            public Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<uint256> RewindAsync()
            {
                throw new NotImplementedException();
            }

            public Task<RewindData> GetRewindData(int height)
            {
                throw new NotImplementedException();
            }

            public Task SaveChangesAsync(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, int height, List<RewindData> rewindDataList = null)
            {
                throw new NotImplementedException();
            }
        }
    }
}
