using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class CoinViewTest
    {
        private TestCoinView coinView;

        public CoinViewTest()
        {
            this.coinView = new TestCoinView();
        }

        [Fact]
        public async Task GetBlockHashAsync_ReturnsBlockHashFromFetchCoinsAsyncMethodAsync()
        {
            uint256 result = await this.coinView.GetTipHashAsync();

            Assert.Equal(new uint256(987263876253), result);
        }

        private class TestCoinView : ICoinView
        {
            public TestCoinView()
            {
            }

            public Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
            {
                var fetchCoinResponse = new FetchCoinsResponse(new UnspentOutputs[0], new uint256(987263876253));
                return Task.FromResult(fetchCoinResponse);
            }

            /// <inheritdoc />
            public async Task<uint256> GetTipHashAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                FetchCoinsResponse response = await this.FetchCoinsAsync(new uint256[0], cancellationToken).ConfigureAwait(false);

                return response.BlockHash;
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
