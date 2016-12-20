using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{	
	public abstract class CoinView
	{
		public async Task<uint256> GetBlockHashAsync()
		{
			var response = await FetchCoinsAsync(new uint256[0]).ConfigureAwait(false);
			return response.BlockHash;
		}
		public abstract Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, uint256 oldBlockHash, uint256 nextBlockHash);
		public abstract Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds);
	}
}
