using Stratis.Bitcoin.BlockPulling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Tests
{
	public class CustomNodeBlockPuller : NodeBlockPuller
	{
		ConcurrentChain _Chain;
		public CustomNodeBlockPuller(ConcurrentChain chain, Node node):base(node)
		{
			_Chain = chain;
		}		

		protected override ConcurrentChain ReloadChainCore()
		{
			return _Chain;
		}
	}
}
