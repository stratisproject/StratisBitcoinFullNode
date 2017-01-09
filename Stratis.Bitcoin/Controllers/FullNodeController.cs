using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Controllers
{
	//TODO: Need to be extensible, should be FullNodeController
	public partial class ConsensusController : Controller
	{
		[ActionName("stop")]
		public Task Stop()
		{
			_FullNode.Dispose();
			return Task.CompletedTask;
		}
	}
}
