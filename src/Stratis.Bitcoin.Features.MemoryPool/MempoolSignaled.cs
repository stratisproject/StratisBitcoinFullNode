using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>Mempool observer on chained header block notifications.</summary>
    public class MempoolSignaled
    {
        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private readonly MempoolSchedulerLock mempoolLock;
        private readonly ITxMempool memPool;
        private readonly IMempoolValidator validator;
        private readonly MempoolOrphans mempoolOrphans;

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

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly ISignals signals;

        /// <summary>
        /// Constructs an instance of a MempoolSignaled object.
        /// Starts the block notification loop to memory pool behaviors for connected nodes.
        /// </summary>
        /// <param name="manager">Memory pool manager injected dependency.</param>
        /// <param name="chain">Concurrent chain injected dependency.</param>
        /// <param name="connection">Connection manager injected dependency.</param>
        /// <param name="nodeLifetime">Node lifetime injected dependency.</param>
        /// <param name="asyncLoopFactory">Asynchronous loop factory injected dependency.</param>
        /// <param name="mempoolLock">The mempool lock.</param>
        /// <param name="memPool">the mempool.</param>
        /// <param name="validator">The mempool validator.</param>
        /// <param name="mempoolOrphans">The mempool orphan list.</param>
        public MempoolSignaled(
            MempoolManager manager,
            ConcurrentChain chain,
            IConnectionManager connection,
            INodeLifetime nodeLifetime,
            IAsyncLoopFactory asyncLoopFactory,
            MempoolSchedulerLock mempoolLock,
            ITxMempool memPool,
            IMempoolValidator validator,
            MempoolOrphans mempoolOrphans,
            ISignals signals)
        {
            this.manager = manager;
            this.chain = chain;
            this.connection = connection;
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;
            this.mempoolLock = mempoolLock;
            this.memPool = memPool;
            this.validator = validator;
            this.mempoolOrphans = mempoolOrphans;
            this.signals = signals;
        }

        /// <summary>
        /// Removes transaction from a block in memory pool.
        /// </summary>
        /// <param name="block">Block of transactions.</param>
        /// <param name="blockHeight">Location of the block.</param>
        public Task RemoveForBlock(Block block, int blockHeight)
        {
            //if (this.IsInitialBlockDownload)
            //  return Task.CompletedTask;

            return this.mempoolLock.WriteAsync(() =>
            {
                this.memPool.RemoveForBlock(block.Transactions, blockHeight);
                this.mempoolOrphans.RemoveForBlock(block.Transactions);

                this.validator.PerformanceCounter.SetMempoolSize(this.memPool.Size);
                this.validator.PerformanceCounter.SetMempoolOrphanSize(this.mempoolOrphans.OrphansCount());
                this.validator.PerformanceCounter.SetMempoolDynamicSize(this.memPool.DynamicMemoryUsage());
            });
        }

        /// <summary>
        /// Announces blocks on all connected nodes memory pool behaviors every five seconds.
        /// </summary>
        public void Start()
        {
            this.signals.OnBlockConnected.Attach(this.OnBlockConnected);

            this.asyncLoop = this.asyncLoopFactory.Run("MemoryPool.RelayWorker", async token =>
            {
                IReadOnlyNetworkPeerCollection peers = this.connection.ConnectedPeers;
                if (!peers.Any())
                    return;

                // Announce the blocks on each nodes behavior which supports relaying.
                IEnumerable<MempoolBehavior> behaviors = peers.Where(x => x.PeerVersion?.Relay ?? false).Select(x => x.Behavior<MempoolBehavior>());
                foreach (MempoolBehavior behavior in behaviors)
                    await behavior.SendTrickleAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.FiveSeconds,
            startAfter: TimeSpans.TenSeconds);
        }

        private void OnBlockConnected(ChainedHeaderBlock chainedHeaderBlock)
        {
            ChainedHeader blockHeader = chainedHeaderBlock.ChainedHeader;

            Task task = this.RemoveForBlock(chainedHeaderBlock.Block, blockHeader?.Height ?? -1);

            // wait for the mempool code to complete
            // until the signaler becomes async
            task.GetAwaiter().GetResult();
        }

        public void Stop()
        {
            this.signals.OnBlockConnected.Detach(this.OnBlockConnected);
            this.asyncLoop?.Dispose();
        }
    }
}
