using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.CoinViews
{
    public class FetchCoinsResponseTest
    {
        [Fact]
        public void Constructor_InitializesClass()
        {
            var blockHash = new uint256(124);
            var unspentOutputs = new UnspentOutputs[] {
                new UnspentOutputs(1, new Transaction()),
                new UnspentOutputs(2, new Transaction())
            };

            var fetchCoinsResponse = new FetchCoinsResponse(unspentOutputs, blockHash);

            Assert.Equal(2, fetchCoinsResponse.UnspentOutputs.Length);
            Assert.Equal((uint)1, fetchCoinsResponse.UnspentOutputs[0].Height);
            Assert.Equal((uint)2, fetchCoinsResponse.UnspentOutputs[1].Height);
            Assert.Equal(blockHash, fetchCoinsResponse.BlockHash);
        }
    }
}
