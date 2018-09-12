using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Notifications.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Notifications
{
    // =================================================================
    // TODO: This class is broken and the logic needs to be redesigned, this effects light wallet.
    // =================================================================

    /// <summary>
    /// Class used to broadcast about new blocks.
    /// </summary>
    public class BlockNotification : IBlockNotification
    {
        /// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        private readonly ILogger logger;

        private readonly IConsensusManager consensusManager;
        private readonly ISignals signals;

        private ChainedHeader tip;

        public BlockNotification(
            ILoggerFactory loggerFactory,
            ConcurrentChain chain,
            IConsensusManager consensusManager,
            ISignals signals,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(consensusManager, nameof(consensusManager));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.Chain = chain;
            this.consensusManager = consensusManager;
            this.signals = signals;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public ConcurrentChain Chain { get; }

        public virtual bool ReSync { get; private set; }

        public virtual uint256 StartHash { get; private set; }

        /// <inheritdoc/>
        public virtual void SyncFrom(uint256 startHash)
        {
            this.logger.LogTrace("Received request to sync from hash : {0}.", startHash);

            // No need to resync the first time this method is called.
            if (this.StartHash != null)
            {
                this.ReSync = true;

                ChainedHeader startBlock = this.Chain.GetBlock(startHash);
                if (startBlock != null)
                {
                    // Sets the location of the puller to the block preceding the one we want to receive.
                    ChainedHeader previousBlock = this.Chain.GetBlock(startBlock.Height > 0 ? startBlock.Height - 1 : 0);
                   // this.Puller.SetLocation(previousBlock);
                    this.tip = previousBlock;

                    this.logger.LogTrace("Puller location set to block: {0}.", previousBlock);
                }
            }

            this.StartHash = startHash;
        }

        /// <inheritdoc/>
        public void Start()
        {
            this.asyncLoop = this.asyncLoopFactory.Run("Notify", async token =>
            {
                await this.Notify(this.nodeLifetime.ApplicationStopping);
            },
            this.nodeLifetime.ApplicationStopping);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (this.asyncLoop != null)
                this.asyncLoop.Dispose();
        }

        internal Task Notify(CancellationToken token)
        {
            // Not syncing until the StartHash has been set.
            if (this.StartHash == null)
                return Task.CompletedTask;

            // Not syncing until the chain is downloaded at least up to this block.
            ChainedHeader startBlock = this.Chain.GetBlock(this.StartHash);
            if (startBlock == null)
                return Task.CompletedTask;

            // Sets the location of the puller to the block preceding the one we want to receive.
            ChainedHeader previousBlock = this.Chain.GetBlock(startBlock.Height > 0 ? startBlock.Height - 1 : 0);
           // this.Puller.SetLocation(previousBlock);
            this.tip = previousBlock;

            this.logger.LogTrace("Puller location set to block: {0}.", previousBlock);

            // Send notifications for all the following blocks.
            while (!this.ReSync)
            {
                token.ThrowIfCancellationRequested();

                //LookaheadResult lookaheadResult = this.Puller.NextBlock(token);
                //if (lookaheadResult.Block != null)
                //{
                //    // Broadcast the block to the registered observers.
                //    this.signals.SignalBlock(lookaheadResult.Block);
                //    this.tip = this.Chain.GetBlock(lookaheadResult.Block.GetHash());

                //    continue;
                //}

                // In reorg we reset the puller to the fork.
                // When a reorg happens the puller is pushed back and continues from the current fork.
                // Find the location of the fork.
                while (this.Chain.GetBlock(this.tip.HashBlock) == null)
                    this.tip = this.tip.Previous;

                // Set the puller to the fork location.
                //this.Puller.SetLocation(this.tip);
            }

            this.ReSync = false;

            return Task.CompletedTask;
        }
    }
}
