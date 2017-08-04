using System.Collections.Concurrent;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.IndexStore
{
    public class IndexStoreSignaled : SignalObserver<Block>
    {
        private readonly IndexStoreLoop storeLoop;
        private readonly ConcurrentChain chain;
        private readonly NodeSettings nodeArgs;
        private readonly ChainState chainState;
        private readonly IConnectionManager connection;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private readonly ConcurrentDictionary<uint256, uint256> blockHashesToAnnounce; // maybe replace with a task scheduler

        public IndexStoreSignaled(IndexStoreLoop storeLoop, ConcurrentChain chain, NodeSettings nodeArgs,
            ChainState chainState, IConnectionManager connection,
            INodeLifetime nodeLifetime, IAsyncLoopFactory asyncLoopFactory)
        {
            this.storeLoop = storeLoop;
            this.chain = chain;
            this.nodeArgs = nodeArgs;
            this.chainState = chainState;
            this.connection = connection;
            this.nodeLifetime = nodeLifetime;
            this.asyncLoopFactory = asyncLoopFactory;

            this.blockHashesToAnnounce = new ConcurrentDictionary<uint256, uint256>();
        }

        protected override void OnNextCore(Block value)
        {
            if (this.nodeArgs.Store.Prune)
                return;

            // ensure the block is written to disk before relaying
            this.storeLoop.AddToPending(value);

            if (this.chainState.IsInitialBlockDownload)
                return;

            this.blockHashesToAnnounce.TryAdd(value.GetHash(), value.GetHash());
        }

        public void RelayWorker()
        {
            this.asyncLoopFactory.Run("IndexStore.RelayWorker", async token =>
            {
                var blocks = this.blockHashesToAnnounce.Keys.ToList();

                if (!blocks.Any())
                    return;

                uint256 outer;
                foreach (var blockHash in blocks)
                    this.blockHashesToAnnounce.TryRemove(blockHash, out outer);

                var nodes = this.connection.ConnectedNodes;
                if (!nodes.Any())
                    return;

                // announce the blocks on each nodes behaviour
                var behaviours = nodes.Select(s => s.Behavior<IndexStoreBehavior>());
                foreach (var behaviour in behaviours)
                    await behaviour.AnnounceBlocks(blocks).ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Second,
            startAfter: TimeSpans.FiveSeconds);
        }
    }
}
