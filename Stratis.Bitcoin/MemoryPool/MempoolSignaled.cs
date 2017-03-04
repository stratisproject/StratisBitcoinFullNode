using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.MemoryPool
{
    public class MempoolSignaled : SignaleObserve<Block>
	{
		private readonly MempoolManager manager;
		private readonly ConcurrentChain chain;
		private readonly ConnectionManager connection;

		public MempoolSignaled(MempoolManager manager, ConcurrentChain chain, ConnectionManager connection, CancellationTokenSource globalCancellationTokenSource)
		{
			this.manager = manager;
			this.chain = chain;
			this.connection = connection;
			this.RelayWorker(globalCancellationTokenSource.Token);
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
			new PeriodicAsyncTask("MemoryPool.RelayWorker", async token =>
			{
				var nodes = this.connection.ConnectedNodes;
				if (!nodes.Any())
					return;

				// announce the blocks on each nodes behaviour
				var behaviours = nodes.Select(s => s.Behavior<MempoolBehavior>());
				foreach (var behaviour in behaviours)
					await behaviour.SendTrickle().ConfigureAwait(false);

			}).StartAsync(cancellationToken, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
		}
	}
}
