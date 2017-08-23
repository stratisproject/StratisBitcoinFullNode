using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class FetchCoinsResponse
    {
        public uint256 BlockHash { get; set; }

        public UnspentOutputs[] UnspentOutputs { get; set; }

        public FetchCoinsResponse()
        {
        }

        public FetchCoinsResponse(UnspentOutputs[] unspent, uint256 blockHash)
        {
            this.BlockHash = blockHash;
            this.UnspentOutputs = unspent;
        }
    }
}
