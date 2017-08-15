using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using System.Linq;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Mempool observer on block notifications.
    /// </summary>
    public class MempoolSignaled : SignalObserver<Block>
    {
        #region Fields

        /// <summary>
        /// Memory pool manager injected dependency.
        /// </summary>
        private readonly MempoolManager manager;

        /// <summary>
        /// Concurrent chain injected dependency.
        /// </summary>
        private readonly ConcurrentChain chain;

        /// <summary>
        /// Connection manager injected dependency.
        /// </summary>
        private readonly IConnectionManager connection;

        /// <summary>
        /// Node lifetime injected dependency.
        /// </summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>
        /// Asynchronous loop factory injected dependency.
        /// </summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs an instance of a MempoolSignaled object.
        /// Starts the block notification loop to memory pool behaviors for connected nodes.
        /// </summary>
        /// <param name="manager">Memory pool manager injected dependency.</param>
        /// <param name="chain">Concurrent chain injected dependency.</param>
        /// <param name="connection">Connection manager injected dependency.</param>
        /// <param name="nodeLifetime">Node lifetime injected dependency.</param>
        /// <param name="asyncLoopFactory">Asynchronous loop factory injected dependency.</param>
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

        #endregion

        #region ObserverBase Overrides

        /// <inheritdoc />
        protected override void OnNextCore(Block value)
        {
            var task = this.manager.RemoveForBlock(value, this.chain.GetBlock(value.GetHash()).Height);

            // wait for the mempool code to complete
            // until the signaler becomes async 
            task.GetAwaiter().GetResult();
        }

        #endregion

        /// <summary>
        /// Announces blocks on all connected nodes memory pool behaviours every ten seconds.
        /// </summary>
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
