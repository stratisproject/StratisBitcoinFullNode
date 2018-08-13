using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class CoinViewTest
    {
        private ICoinView coinView;

        public CoinViewTest()
        {
            var coinViewMock = new Mock<ICoinView>();
            var fetchCoinResponse = new FetchCoinsResponse(new UnspentOutputs[0], new uint256(987263876253));
            coinViewMock.Setup(c => c.FetchCoinsAsync(It.IsAny<uint256[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fetchCoinResponse);
            coinViewMock.Setup(c => c.GetTipHashAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(fetchCoinResponse.BlockHash);
            this.coinView = coinViewMock.Object;
        }

        [Fact]
        public async Task GetBlockHashAsync_ReturnsBlockHashFromFetchCoinsAsyncMethodAsync()
        {
            uint256 result = await this.coinView.GetTipHashAsync();

            Assert.Equal(new uint256(987263876253), result);
        }
    }
}
