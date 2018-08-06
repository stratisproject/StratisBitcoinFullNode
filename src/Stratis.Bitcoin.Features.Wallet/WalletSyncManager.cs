using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletSyncManager : IWalletSyncManager
    {
        private readonly IWalletManager walletManager;

        /// <summary>Thread safe class representing a chain of headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IBlockStore blockStore;

        private readonly StoreSettings storeSettings;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        protected ChainedHeader walletTip;

        public ChainedHeader WalletTip => this.walletTip;

        /// <summary>Provides a <see cref="BufferBlock{T}"/> for storing <see cref="Block"/> data</summary>
        private BufferBlock<Block> BlockBuffer { get; }

        /// <summary>Provides a <see cref="Block"/> <see cref="ConcurrentQueue{T}"/></summary>
        private readonly ConcurrentQueue<Block> blocksQueue;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        public WalletSyncManager(ILoggerFactory loggerFactory, IWalletManager walletManager, ConcurrentChain chain,
            Network network, IBlockStore blockStore, StoreSettings storeSettings, INodeLifetime nodeLifetime, 
            IAsyncLoopFactory asyncLoopFactory)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(blockStore, nameof(blockStore));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

            this.walletManager = walletManager;
            this.chain = chain;
            this.blockStore = blockStore;
            this.storeSettings = storeSettings;
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.asyncLoopFactory = asyncLoopFactory;

            this.BlockBuffer = new BufferBlock<Block>();
            this.blocksQueue = new ConcurrentQueue<Block>();
        }

        /// <inheritdoc />
        public void Start()
        {
            this.logger.LogTrace("()");

            // When a node is pruned it impossible to catch up
            // if the wallet falls behind the block puller.
            // To support pruning the wallet will need to be
            // able to download blocks from peers to catch up.
            if (this.storeSettings.Prune)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation("WalletSyncManager initialized. Wallet at block {0}.", this.walletManager.LastBlockHeight());

            this.walletTip = this.chain.GetBlock(this.walletManager.WalletTipHash);
            if (this.walletTip == null)
            {
                // The wallet tip was not found in the main chain.
                // this can happen if the node crashes unexpectedly.
                // To recover we need to find the first common fork
                // with the best chain. As the wallet does not have a
                // list of chain headers, we use a BlockLocator and persist
                // that in the wallet. The block locator will help finding
                // a common fork and bringing the wallet back to a good
                // state (behind the best chain).
                ICollection<uint256> locators = this.walletManager.GetFirstWalletBlockLocator();
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chain.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                this.walletManager.WalletTipHash = fork.HashBlock;
                this.walletTip = fork;
            }

            this.asyncLoopFactory.Run(nameof(WalletSyncManager), async token =>
            {
                await this.ProcessBlockLoopAsync(token).ConfigureAwait(false);
            }, 
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.TenSeconds);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Stop()
        {
            this.logger.LogTrace("()");
            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void ProcessBlock(Block block)
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            this.QueueBlock(block);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public virtual void ProcessTransaction(Transaction transaction)
        {
            this.logger.LogTrace("()");

            Guard.NotNull(transaction, nameof(transaction));

            this.logger.LogTrace("({0}:'{1}')", nameof(transaction), transaction.GetHash());

            this.walletManager.ProcessTransaction(transaction);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public virtual void SyncFromDate(DateTime date)
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("({0}:'{1::yyyy-MM-dd HH:mm:ss}')", nameof(date), date);

            int blockSyncStart = this.chain.GetHeightAtTime(date);
            this.SyncFromHeight(blockSyncStart);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public virtual void SyncFromHeight(int height)
        {
            this.logger.LogTrace("()");

            this.logger.LogTrace("({0}:{1})", nameof(height), height);

            ChainedHeader chainedHeader = this.chain.GetBlock(height);
            this.walletTip = chainedHeader ?? throw new WalletException("Invalid block height");
            this.walletManager.WalletTipHash = chainedHeader.HashBlock;

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Processes a new block.
        /// </summary>
        /// <param name="block"><see cref="Block"/> to process</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task ProcessAsync(Block block)
        {
            await Task.Run(() =>
            {
                this.logger.LogTrace("()");

                Guard.NotNull(block, nameof(block));
                this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

                ChainedHeader newTip = this.chain.GetBlock(block.GetHash());
                if (newTip == null)
                {
                    this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                    return;
                }

                this.logger.LogTrace("({0}:'{1}')", nameof(newTip), newTip.ToString());

                // If the new block's previous hash is the same as the
                // wallet hash then just pass the block to the manager.
                if (block.Header.HashPrevBlock != this.walletTip.HashBlock)
                {
                    // If previous block does not match there might have
                    // been a reorg, check if the wallet is still on the main chain.
                    ChainedHeader inBestChain = this.chain.GetBlock(this.walletTip.HashBlock);
                    if (inBestChain == null)
                    {
                        // The current wallet hash was not found on the main chain.
                        // A reorg happened so bring the wallet back top the last known fork.
                        ChainedHeader fork = this.walletTip;

                        // We walk back the chained block object to find the fork.
                        while (this.chain.GetBlock(fork.HashBlock) == null)
                            fork = fork.Previous;

                        this.logger.LogInformation("Reorg detected, going back from '{0}' to '{1}'.", this.walletTip, fork);

                        this.walletManager.RemoveBlocks(fork);
                        this.walletTip = fork;

                        this.logger.LogTrace("Wallet tip set to '{0}'.", this.walletTip);
                    }

                    // The new tip can be ahead or behind the wallet.
                    // If the new tip is ahead we try to bring the wallet up to the new tip.
                    // If the new tip is behind we just check the wallet and the tip are in the same chain.
                    if (newTip.Height > this.walletTip.Height)
                    {
                        ChainedHeader findTip = this.walletTip.FindAncestorOrSelf(newTip);
                        if (findTip == null)
                        {
                            this.logger.LogTrace("(-)[NEW_TIP_BEHIND_NOT_IN_WALLET]");
                            return;
                        }

                        this.logger.LogTrace("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.", this.walletTip, newTip);
                    }
                }
                else
                {
                    this.logger.LogTrace("New block follows the previously known block '{0}'.", this.walletTip);
                }

                this.walletTip = newTip;
                this.walletManager.ProcessBlock(block, newTip);

                this.logger.LogTrace("(-)");
            });
        }

        /// <summary>
        /// Processes blocks stored in the block store cache and block queue, asynchronously.
        /// </summary>
        /// <param name="token">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        private async Task ProcessBlockLoopAsync(CancellationToken token)
        {
            this.logger.LogTrace("()");

            try
            {
                ChainedHeader tip = this.chain.Tip;

                this.logger.LogTrace("({0}:'{1}')", nameof(tip), tip.ToString());

                // if not up-to-date then get previous blocks and sync
                if (tip.Height > this.walletTip.Height)
                {
                    this.logger.LogDebug("(-)[TIP_HEIGHT_>_WALLET_TIP]");

                    ChainedHeader findTip = tip.FindAncestorOrSelf(this.walletTip);
                    if (findTip == null)
                    {
                        this.logger.LogTrace("(-)[NEW_TIP_AHEAD_NOT_IN_WALLET]");
                        return;
                    }

                    this.logger.LogTrace("Wallet tip '{0}' is behind the new tip '{1}'.", this.walletTip, tip);

                    ChainedHeader next = this.walletTip;

                    while (next.Height < tip.Height)
                    {
                        token.ThrowIfCancellationRequested();

                        next = tip.GetAncestor(next.Height + 1);

                        while (true)
                        {
                            token.ThrowIfCancellationRequested();

                            Block nextBlock = this.blockStore.GetBlockAsync(next.HashBlock).GetAwaiter().GetResult();

                            if (nextBlock == null)
                            {
                                // Check if any Blocks are queued up.
                                while (this.blocksQueue.TryDequeue(out Block block))
                                {
                                    this.logger.LogDebug("Process block from blocksQueue - '{0}'", this.chain.GetBlock(block.GetHash()));
                                    await this.ProcessAsync(block).ConfigureAwait(false);
                                }

                                continue;
                            }
                            else
                            {
                                this.logger.LogDebug("Process block from blockStore - '{0}'", this.chain.GetBlock(nextBlock.GetHash()));
                                await this.ProcessAsync(nextBlock).ConfigureAwait(false);
                            }

                            break;
                        }
                    }
                }
                else
                {
                    this.logger.LogDebug("(-)[TIP_HEIGHT_<_WALLET_TIP]");

                    // Check if any Blocks are queued up.
                    while (this.blocksQueue.TryDequeue(out Block block))
                    {
                        this.logger.LogTrace("Process block from blocksQueue - '{0}'", this.chain.GetBlock(block.GetHash()));
                        await this.ProcessAsync(block).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogInformation("Stopping WalletSynceManager...");
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        private void ProducerBlock(ITargetBlock<Block> target, Block block)
        {
            this.logger.LogDebug("()");

            target.Post(block);

            this.logger.LogDebug("(-)");
        }

        /// <inheritdoc />
        private async Task ConsumerBlockAsync(ISourceBlock<Block> source, ConcurrentQueue<Block> queue)
        {
            this.logger.LogDebug("()");

            while (await source.OutputAvailableAsync().ConfigureAwait(false))
            {
                Block block = source.Receive();
                queue.Enqueue(block);
            }

            this.logger.LogDebug("(-)");
        }

        /// <inheritdoc />
        private void QueueBlock(Block block)
        {
            this.logger.LogDebug("()");

            Task.Run(() => this.ConsumerBlockAsync(this.BlockBuffer, this.blocksQueue).ConfigureAwait(false));
        
            this.ProducerBlock(this.BlockBuffer, block);

            this.logger.LogDebug("(-)");
        }
    }
}
