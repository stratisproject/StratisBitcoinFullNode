using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Collections.Concurrent;
using NBitcoin.Protocol.Behaviors;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling
{

	public class StoreBlockPuller : BlockPuller
	{
		public StoreBlockPuller(ConcurrentChain chain, Connection.ConnectionManager nodes) 
			: base(chain, nodes.ConnectedNodes)
		{
		}

		public void AskBlock(ChainedBlock downloadRequest)
		{
			base.AskBlocks(new ChainedBlock[] { downloadRequest });
		}

		public bool TryGetBlock(ChainedBlock chainedBlock, out DownloadedBlock block)
		{
			if (this.DownloadedBlocks.TryRemove(chainedBlock.HashBlock, out block))
			{
				return true;
			}

			this.OnStalling(chainedBlock);
			return false;
		}
	}

}
