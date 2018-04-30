using System;
using System.Collections.Generic;
using System.Text;
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
            var result = await this.coinView.GetBlockHashAsync();

            Assert.Equal(new uint256(987263876253), result);
        }

        private class TestCoinView : CoinView
        {
            public TestCoinView()
            {
            }

            public override Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
            {
                var fetchCoinResponse = new FetchCoinsResponse(new UnspentOutputs[0], new uint256(987263876253));
                return Task.FromResult(fetchCoinResponse);
            }

            public override Task<uint256> Rewind()
            {
                throw new NotImplementedException();
            }

            public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
            {
                throw new NotImplementedException();
            }
        }
    }
}
