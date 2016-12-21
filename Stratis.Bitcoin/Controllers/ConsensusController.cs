using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;

namespace Stratis.Bitcoin.Controllers
{
    public class ConsensusController : Controller
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
			return await _FullNode.CoinView.GetBlockHashAsync();
		}
	}
}
