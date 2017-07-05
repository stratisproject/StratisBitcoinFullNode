using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolSignaled : SignalObserver<Block>
	{
		private readonly MempoolManager manager;
		private readonly ConcurrentChain chain;
		private readonly IConnectionManager connection;
	    private readonly INodeLifetime nodeLifetime;
	    private readonly IAsyncLoopFactory asyncLoopFactory;

	    public MempoolSignaled(MempoolManager manager, ConcurrentChain chain, IConnectionManager connection, 
            INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
		{
			this.manager = manager;
			this.chain = chain;
			this.connection = connection;
		    this.nodeLifetime = nodeLifetime;
		    this.asyncLoopFactory = asyncLoopFactory;
		    this.RelayWorker();
		}

		protected override void OnNextCore(Block value)
		{
			var task = this.manager.RemoveForBlock(value, this.chain.GetBlock(value.GetHash()).Height);

			// wait for the mempool code to complete
			// until the signaler becomes async 
			task.GetAwaiter().GetResult();
		}

		private void RelayWorker()
		{
			this.asyncLoopFactory.Run("MemoryPool.RelayWorker", async token =>
			{
				var nodes = this.connection.ConnectedNodes;
				if (!nodes.Any())
					return;

				// announce the blocks on each nodes behaviour
				var behaviours = nodes.Select(s => s.Behavior<MempoolBehavior>());
				foreach (var behaviour in behaviours)
					await behaviour.SendTrickle().ConfigureAwait(false);
			},
			this.nodeLifetime.ApplicationStopping,
			repeatEvery: TimeSpans.TenSeconds,
			startAfter: TimeSpans.TenSeconds);
		}
	}
}
