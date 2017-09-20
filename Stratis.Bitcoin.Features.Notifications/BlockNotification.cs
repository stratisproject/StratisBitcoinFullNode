using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Notifications
{
    /// <summary>
    /// Class used to broadcast about new blocks.
    /// </summary>
    public class BlockNotification
    {
        private readonly ISignals signals;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger logger;
        private ChainedBlock tip;

        public BlockNotification(
            ILoggerFactory loggerFactory,
            ConcurrentChain chain,
            ILookaheadBlockPuller puller,
            ISignals signals,
            IAsyncLoopFactory asyncLoopFactory,
            INodeLifetime nodeLifetime)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(puller, nameof(puller));
            Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.Chain = chain;
            this.Puller = puller;
            this.signals = signals;
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public ILookaheadBlockPuller Puller { get; }

        public ConcurrentChain Chain { get; }

        public uint256 StartHash { get; private set; }

        private bool reSync;

        public virtual void SyncFrom(uint256 startHash)
        {
            this.logger.LogTrace("Received request to sync from hash : {0}.", startHash);

            // No need to resync the first time this method is called.
            if (this.StartHash != null)
            {
                this.reSync = true;

                ChainedBlock startBlock = this.Chain.GetBlock(startHash);
                if (startBlock != null)
                {
                    // Sets the location of the puller to the block preceding the one we want to receive.
                    ChainedBlock previousBlock = this.Chain.GetBlock(startBlock.Height > 0 ? startBlock.Height - 1 : 0);
                    this.Puller.SetLocation(previousBlock);
                    this.tip = previousBlock;

                    this.logger.LogTrace("Puller location set to block: {0}.", previousBlock);
                }
            }

            this.StartHash = startHash;
        }

        /// <summary>
        /// Notifies about blocks, starting from block with hash passed as parameter.
        /// </summary>
        public virtual IAsyncLoop Notify()
        {
            return this.asyncLoopFactory.Run("block notifier", token =>
            {
                // Not syncing until the StartHash has been set.
                if (this.StartHash == null)
                {
                    return Task.CompletedTask;
                }

                // Not syncing until the chain is downloaded at least up to this block.
                ChainedBlock startBlock = this.Chain.GetBlock(this.StartHash);
                if (startBlock == null)
                {
                    return Task.CompletedTask;
                }

                // Sets the location of the puller to the block preceding the one we want to receive.
                ChainedBlock previousBlock = this.Chain.GetBlock(startBlock.Height > 0 ? startBlock.Height - 1 : 0);
                this.Puller.SetLocation(previousBlock);
                this.tip = previousBlock;

                this.logger.LogTrace("Puller location set to block: {0}.", previousBlock);

                // Send notifications for all the following blocks.
                while (!this.reSync)
                {
                    var block = this.Puller.NextBlock(token);
                    if (block != null)
                    {
                        // Broadcast the block to the registered observers.
                        this.signals.SignalBlock(block);
                        this.tip = this.Chain.GetBlock(block.GetHash());
                    }
                    else
                    {
                        // In reorg we reset the puller to the fork.
                        // When a reorg happens the puller is pushed back and continues from the current fork.
                        // Find the location of the fork.
                        while (this.Chain.GetBlock(this.tip.HashBlock) == null)
                            this.tip = this.tip.Previous;

                        // Set the puller to the fork location.
                        this.Puller.SetLocation(this.tip);
                    }
                }

                this.reSync = false;

                return Task.CompletedTask;
            }, this.nodeLifetime.ApplicationStopping);
        }
    }
}
