using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;

namespace Stratis.Bitcoin.Controllers
{
    public partial class ConsensusController : Controller
	{
		public ConsensusController(FullNode fullNode)
		{
			_FullNode = fullNode;
		}
		FullNode _FullNode;

		[ActionName("getbestblockhash")]
		public async Task<uint256> GetBestBlockHash()
		{
			return await _FullNode.CoinView.GetBlockHashAsync();
		}

		[ActionName("getblockhash")]
		public async Task<uint256> GetBlockHash(int height)
		{
			var bestBlockHash = await _FullNode.CoinView.GetBlockHashAsync();
			var bestBlock = _FullNode.Chain.GetBlock(bestBlockHash);
			if(bestBlock == null)
				return null;
			var block = _FullNode.Chain.GetBlock(height);
			return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
		}
	}
}
