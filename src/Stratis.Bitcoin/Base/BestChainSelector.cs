using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly AsyncQueue<ChainedBlock> unavailableTipsProcessingQueue;

        /// <summary>Collection of all available tips provided by connected peers.</summary>
        private readonly Dictionary<int, ChainedBlock> availableTips;
        
        /// <summary>Information about node's chain.</summary>
        private readonly IChainState chainState;

        /// <summary>Protects access to <see cref="availableTips"/>.</summary>
        private readonly object lockObject;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Creates new instance of <see cref="BestChainSelector"/>
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
            this.availableTips = new Dictionary<int, ChainedBlock>();

            this.unavailableTipsProcessingQueue = new AsyncQueue<ChainedBlock>((tip, token) =>
            {
                this.logger.LogTrace("({0}:'{1}')", nameof(tip), tip);

                // Ignore it if it wasn't the best chain's tip.
                if (tip != this.chain.Tip)
                {
                    this.logger.LogTrace("(-)[NOT_BEST_CHAIN_TIP]");
                    return Task.CompletedTask;
                }

                lock (this.lockObject)
                {
                    // If better tip is not found consensus tip should be used.
                    ChainedBlock bestTip = this.chainState.ConsensusTip;

                    // Find best tip from available ones.
                    foreach (ChainedBlock availableTip in this.availableTips.Values)
                    {
                        if (availableTip == this.chain.Tip)
                        {
                            // Do nothing if there is at least one available tip that is equal to the best chain's tip. 
                            this.logger.LogTrace("(-)[EQUIVALENT_TIP_FOUND]");

                            return Task.CompletedTask;
                        }

                        if (bestTip.ChainWork < availableTip.ChainWork)
                            bestTip = availableTip;
                    }

                    this.chain.SetTip(bestTip);
                }

                this.logger.LogTrace("(-)");
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Sets available tip if it doesn't violate the max reorg protection rule.
        /// </summary>
        /// <param name="peerConnectionId">The peer connection id.</param>
        /// <param name="tip">The tip.</param>
        /// <returns>
        /// <c>true</c> if the tip was added to the available tips collection, 
        /// <c>false</c> if it's invalid and violates the max reorg rule.
        /// </returns>
        public bool TrySetAvailableTip(int peerConnectionId, ChainedBlock tip)
        {
            Guard.NotNull(tip, nameof(tip));
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peerConnectionId), peerConnectionId, nameof(tip), tip);

            bool switchToNewTip = false;

            if (tip.ChainWork > this.chain.Tip.ChainWork)
            {
                // Long reorganization protection on POS networks.
                switchToNewTip = true;
                uint maxReorgLength = this.chainState.MaxReorgLength;
                ChainedBlock consensusTip = this.chainState.ConsensusTip;
                if ((maxReorgLength != 0) && (consensusTip != null))
                {
                    ChainedBlock fork = tip.FindFork(consensusTip);

                    if ((fork != null) && (fork != consensusTip))
                    {
                        int reorgLength = consensusTip.Height - fork.Height;

                        if (reorgLength > maxReorgLength)
                        {
                            this.logger.LogTrace("Reorganization of length {0} prevented, maximal reorganization length is {1}, consensus tip is '{2}'.", reorgLength, maxReorgLength, consensusTip);
                            this.logger.LogTrace("(-)[MAX_REORG_VIOLATION]");
                            return false;
                        }
                        else
                            this.logger.LogTrace("Reorganization of length {0} accepted, consensus tip is '{1}'.", reorgLength, consensusTip);
                    }
                }
            }

            lock (this.lockObject)
            {
                if (switchToNewTip)
                {
                    this.logger.LogTrace("New chain tip '{0}' selected, chain work is '{1}'.", tip, tip.ChainWork);

                    if (this.chain.SetTipIfChainworkIsGreater(tip))
                    {
                        // This allows garbage collection to collect the duplicated pendingTip and ancestors.
                        ChainedBlock chainedTip = this.chain.GetBlock(tip.HashBlock);

                        if (chainedTip != null)
                            tip = chainedTip;
                    }
                }

                this.availableTips.AddOrReplace(peerConnectionId, tip);
            }

            this.logger.LogTrace("(-)");
            return true;
        }

        /// <summary>
        /// Removes tip associated with the provided peer connection ID.
        /// </summary>
        /// <param name="peerConnectionId">The peer connection id.</param>
        public void RemoveAvailableTip(int peerConnectionId)
        {
            this.logger.LogTrace("()");

            lock (this.lockObject)
            {
                if (this.availableTips.TryGetValue(peerConnectionId, out ChainedBlock tip))
                {
                    this.availableTips.Remove(peerConnectionId);
                    this.unavailableTipsProcessingQueue.Enqueue(tip);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");
            
            this.unavailableTipsProcessingQueue.Dispose();

            // Switch to the consensus tip. 
            this.chain.SetTip(this.chainState.ConsensusTip);

            this.logger.LogTrace("(-)");
        }
    }
}
