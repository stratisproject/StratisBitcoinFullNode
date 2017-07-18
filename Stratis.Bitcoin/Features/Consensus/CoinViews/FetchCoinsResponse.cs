using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public class FetchCoinsResponse
    {
		public FetchCoinsResponse()
		{

		}
		public FetchCoinsResponse(UnspentOutputs[] unspent, uint256 blockHash)
		{
			this.BlockHash = blockHash;
            this.UnspentOutputs = unspent;
		}
		public UnspentOutputs[] UnspentOutputs
		{
			get; set;
		}
		public uint256 BlockHash
		{
			get; set;
		}
	}
}
