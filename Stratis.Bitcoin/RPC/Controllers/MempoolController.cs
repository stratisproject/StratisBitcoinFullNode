using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Controllers
{
	public  class MempoolController : Controller
	{
		public MempoolController(FullNode fullNode)
		{
			_FullNode = fullNode;
		}
		FullNode _FullNode;
		[ActionName("getrawmempool")]
		public Task<List<uint256>> GetRawMempool()
		{
			return _FullNode.MempoolManager.GetMempoolAsync();
		}
	}
}
