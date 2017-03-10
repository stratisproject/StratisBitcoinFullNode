using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
    public class MempoolSignaled : SignalObserver<Block>
	{
		private readonly MempoolManager manager;
		private readonly ConcurrentChain chain;
		private readonly ConnectionManager connection;

		public MempoolSignaled(MempoolManager manager, ConcurrentChain chain, ConnectionManager connection, FullNode.CancellationProvider cancellationProvider)
		{
			this.manager = manager;
			this.chain = chain;
			this.connection = connection;
			this.RelayWorker(cancellationProvider.Cancellation.Token);
		}

		protected override void OnNextCore(Block value)
		{
			var task = this.manager.RemoveForBlock(value, this.chain.GetBlock(value.GetHash()).Height);

			// wait for the mempool code to complete
			// until the signaler becomes async 
			task.GetAwaiter().GetResult();
		}

		private void RelayWorker(CancellationToken cancellationToken)
		{
            AsyncLoop.Run("MemoryPool.RelayWorker", async token =>
			{
				var nodes = this.connection.ConnectedNodes;
				if (!nodes.Any())
					return;

				// announce the blocks on each nodes behaviour
				var behaviours = nodes.Select(s => s.Behavior<MempoolBehavior>());
				foreach (var behaviour in behaviours)
					await behaviour.SendTrickle().ConfigureAwait(false);
            },
            cancellationToken,
            repeatEvery: TimeSpans.TenSeconds,
            startAfter: TimeSpans.TenSeconds);
		}
	}
}
