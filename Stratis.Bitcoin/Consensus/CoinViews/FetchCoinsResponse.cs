using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
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
