using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{	
	public abstract class CoinView
	{
		public async Task<uint256> GetBlockHashAsync()
		{
			var response = await this.FetchCoinsAsync(new uint256[0]).ConfigureAwait(false);
			return response.BlockHash;
		}
		public abstract Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash);
		public abstract Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds);

		public abstract Task<uint256> Rewind();
	}
}
