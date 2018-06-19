using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Selects the best available chain based on tips provided by the peers and switches to it.
    /// </summary>
    /// <remarks>
    /// When a peer that provided the best chain is disconnected we select the best chain that is backed by one of the connected
    /// peers and switch to it since we are not interested in a chain that is not represented by any peer.
    /// </remarks>
    public class BestChainSelector : IDisposable
    {
        /// <summary>Thread safe class representing a chain of headers from genesis.</summary>
        private readonly ConcurrentChain chain;

        /// <summary>Queue with tips from the disconnected peers.</summary>
        private readonly AsyncQueue<ChainedHeader> unavailableTipsProcessingQueue;

        /// <summary>Collection of all available tips provided by connected peers.</summary>
        private readonly Dictionary<int, ChainedHeader> availableTips;
        
        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Protects access to <see cref="availableTips"/>.</summary>
        private readonly object lockObject;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Creates new instance of <see cref="BestChainSelector"/>.
        /// </summary>
        /// <param name="chain">Thread safe class representing a chain of headers from genesis.</param>
        /// <param name="chainState">Information about node's chain.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public BestChainSelector(ConcurrentChain chain, IChainState chainState, ILoggerFactory loggerFactory)
        {
            this.chain = chain;
            this.chainState = chainState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.lockObject = new object();
            this.availableTips = new Dictionary<int, ChainedHeader>();

            this.unavailableTipsProcessingQueue = new AsyncQueue<ChainedHeader>(this.OnEnqueueAsync);
        }

        /// <summary>Called when peer disconnects.</summary>
        /// <param name="tip">Tip that used to belong to a disconnected peer.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private Task OnEnqueueAsync(ChainedHeader tip, CancellationToken cancellationToken)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(tip), tip);

            lock (this.lockObject)
            {
                // Ignore it if it wasn't the best chain's tip.
                if (tip != this.chain.Tip)
                {
                    this.logger.LogTrace("(-)[NOT_BEST_CHAIN_TIP]");
                    return Task.CompletedTask;
                }

                // If better tip is not found consensus tip should be used.
                ChainedHeader bestTip = this.chainState.ConsensusTip;

                // Find best tip from available ones.
                foreach (ChainedHeader availableTip in this.availableTips.Values)
                {
                    if (availableTip == this.chain.Tip)
                    {
                        // Do nothing if there is at least one available tip that is equal to the best chain's tip. 
                        this.logger.LogTrace("(-)[EQUIVALENT_TIP_FOUND]");
                        return Task.CompletedTask;
                    }

                    // We need to check max reorg here again because it is possible that since the last check our tip has advanced and now 
                    // available tip claims a reorg of length that is longer than maximum allowed.
                    if ((bestTip.ChainWork < availableTip.ChainWork) && !this.IsMaxReorgRuleViolated(availableTip))
                        bestTip = availableTip;
                }

                this.chain.SetTip(bestTip);
            }

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets available tip if it doesn't violate the max reorg protection rule.
        /// </summary>
        /// <param name="peerConnectionId">Unique ID of the peer's connection.</param>
        /// <param name="tip">The tip.</param>
        /// <returns>
        /// <c>true</c> if the tip was added to the available tips collection, 
        /// <c>false</c> if it's invalid and violates the max reorg rule.
        /// </returns>
        public bool TrySetAvailableTip(int peerConnectionId, ChainedHeader tip)
        {
            Guard.NotNull(tip, nameof(tip));
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(peerConnectionId), peerConnectionId, nameof(tip), tip);
            
            if (this.IsMaxReorgRuleViolated(tip))
            {
                this.logger.LogTrace("(-)[MAX_REORG_VIOLATION]:false");
                return false;
            }
            
            lock (this.lockObject)
            {
                if (this.chain.SetTipIfChainworkIsGreater(tip))
                {
                    this.logger.LogTrace("New chain tip '{0}' selected, chain work is '{1}'.", tip, tip.ChainWork);

                    // This allows garbage collection to collect the duplicated tip and it's ancestors.
                    tip = this.chain.GetBlock(tip.HashBlock) ?? tip;
                }

                this.availableTips.AddOrReplace(peerConnectionId, tip);
            }

            this.logger.LogTrace("(-):true");
            return true;
        }

        /// <summary>Checks if <paramref name="tip"/> violates the max reorg rule for POS networks.</summary>
        /// <param name="tip">The tip.</param>
        /// <returns><c>true</c> if maximum reorg rule violated, <c>false</c> otherwise.</returns>
        private bool IsMaxReorgRuleViolated(ChainedHeader tip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(tip), tip);

            uint maxReorgLength = this.chainState.MaxReorgLength;
            ChainedHeader consensusTip = this.chainState.ConsensusTip;
            if ((maxReorgLength != 0) && (consensusTip != null))
            {
                ChainedHeader fork = tip.FindFork(consensusTip);

                if (fork == null)
                {
                    this.logger.LogError("Header '{0}' is from a different network.", tip);
                    this.logger.LogTrace("(-)[HEADER_IS_INVALID_NETWORK]");
                    throw new InvalidOperationException("Header is from a different network");
                }

                if ((fork != tip) && (fork != consensusTip))
                {
                    int reorgLength = consensusTip.Height - fork.Height;

                    if (reorgLength > maxReorgLength)
                    {
                        this.logger.LogTrace("Reorganization of length {0} prevented, maximal reorganization length is {1}, consensus tip is '{2}'.", reorgLength, maxReorgLength, consensusTip);
                        this.logger.LogTrace("(-):true");
                        return true;
                    }

                    this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                }
            }

            this.logger.LogTrace("(-):false");
            return false;
        }

        /// <summary>
        /// Removes tip associated with the provided peer connection ID.
        /// </summary>
        /// <param name="peerConnectionId">Unique ID of the peer's connection.</param>
        public void RemoveAvailableTip(int peerConnectionId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerConnectionId), peerConnectionId);

            lock (this.lockObject)
            {
                if (this.availableTips.TryGetValue(peerConnectionId, out ChainedHeader tip))
                {
                    this.availableTips.Remove(peerConnectionId);
                    this.unavailableTipsProcessingQueue.Enqueue(tip);

                    this.logger.LogTrace("Available tip '{0}' for peer connection id {1} was removed.", tip, peerConnectionId);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");
            
            this.unavailableTipsProcessingQueue.Dispose();
            
            this.logger.LogTrace("(-)");
        }
    }
}
