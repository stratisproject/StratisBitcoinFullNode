using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Selects the best available chain based on Tips provided by the peers and switches to it.
    /// </summary>
    /// <remarks>
    /// When a peer that provided the best chain is disconnected we select the best chain that is backed by one of the connected
    /// peers and switch to it since we are not interested in a chain that is not represented by any peer.
    /// </remarks>
    public class BestChainSelector : IDisposable
    {
        private readonly ConcurrentChain chain;

        private readonly AsyncQueue<ChainedBlock> unavailableTipsProcessingQueue;

        private Task dequeueTask;

        private readonly Dictionary<int, ChainedBlock> availableTips;

        private readonly CancellationTokenSource cancellation;

        /// <summary>Protects access to <see cref="availableTips"/>.</summary>
        private readonly object lockObject;

        /// <summary>
        /// Creates new instance of <see cref="BestChainSelector"/>
        /// </summary>
        public BestChainSelector(ConcurrentChain chain)
        {
            this.chain = chain;

            this.lockObject = new object();
            this.availableTips = new Dictionary<int, ChainedBlock>();
            this.unavailableTipsProcessingQueue = new AsyncQueue<ChainedBlock>();
            this.cancellation = new CancellationTokenSource();

            //move max reorg and chain switching here
            //chb provides a tip and we answer if its ok or peer should be banned
        }

        public void Initialize()
        {
            this.dequeueTask = Task.Run(async () =>
            {
                while (!this.cancellation.IsCancellationRequested)
                {
                    try
                    {
                        // tip or a peer that was disconnected
                        ChainedBlock tip = await this.unavailableTipsProcessingQueue.DequeueAsync(this.cancellation.Token).ConfigureAwait(false);

                        if (tip != this.chain.Tip)
                            continue;
                    }
                    catch (OperationCanceledException)
                    {
                        continue;
                    }

                    lock (this.lockObject)
                    {
                        ChainedBlock bestTip = this.availableTips.Aggregate((item1, item2) => item1.Value.ChainWork > item2.Value.ChainWork ? item1 : item2).Value;

                        if (bestTip != this.chain.Tip)
                            this.chain.SetTip(bestTip);
                    }
                }
            });
        }
        
        public void SetAvailableTip(int peerConnectionId, ChainedBlock tip)
        {
            lock (this.lockObject)
            {
                this.availableTips.AddOrReplace(peerConnectionId, tip);
            }
        }

        public void RemoveAvailableTip(int peerConnectionId)
        {
            lock (this.lockObject)
            {
                if (this.availableTips.TryGetValue(peerConnectionId, out ChainedBlock tip))
                {
                    this.availableTips.Remove(peerConnectionId);
                    this.unavailableTipsProcessingQueue.Enqueue(tip);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellation.Cancel();

            this.dequeueTask?.Wait();

            this.unavailableTipsProcessingQueue.Dispose();
        }
    }
}
