using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.BlockPulling
{
    public interface ILookaheadBlockPuller
    {
        /// <summary>
        /// Tries to retrieve a block that is in front of the current puller's position by a specific height.
        /// </summary>
        /// <param name="count">How many blocks ahead (minus one) should the returned block be ahead of the current puller's position. 
        /// A value of zero will provide the next block, a value of one will provide a block that is 2 blocks ahead.</param>
        /// <returns>The block which height is <paramref name="count"/>+1 higher than current puller's position, 
        /// or null if such a block is not downloaded or does not exist.</returns>
        Block TryGetLookahead(int count);

        /// <summary>Sets the current location of the puller to a specific block header.</summary>
        /// <param name="location">Block header to set the location to.</param>
        void SetLocation(ChainedBlock location);

        /// <summary>
        /// Waits for a next block to be available (downloaded).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to allow the caller to cancel waiting for the next block.</param>
        /// <returns>Next block or null if a reorganization happened on the chain.</returns>
        Block NextBlock(CancellationToken cancellationToken);

        /// <summary>
        /// Adds a specific requirement to all peer nodes.
        /// </summary>
        /// <param name="transactionOptions">Specifies the requirement on nodes to add.</param>
        void RequestOptions(TransactionOptions transactionOptions);
    }

    /// <summary>
    /// Puller with ability to  lookahead calculates the next blocks to download
    /// </summary>
    public class LookaheadBlockPuller : BlockPuller, ILookaheadBlockPuller
    {
        /// <summary>Maximal size of a block in bytes.</summary>
        private const int BLOCK_SIZE = 2000000;

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a connection manager. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="connectionManager">Manager of information about the node's network connections.</param>
        public LookaheadBlockPuller(ConcurrentChain chain, IConnectionManager connectionManager)
            : base(chain, connectionManager.ConnectedNodes, connectionManager.NodeSettings.ProtocolVersion)
        {
            this.MaxBufferedSize = BLOCK_SIZE * 10;
            this.MinimumLookahead = 4;
            this.MaximumLookahead = 2000;
        }

        public int MinimumLookahead
        {
            get;
            set;
        }

        public int MaximumLookahead
        {
            get;
            set;
        }

        private int actualLookahead;
        public int ActualLookahead
        {
            get
            {
                return Math.Min(this.MaximumLookahead, Math.Max(this.MinimumLookahead, this.actualLookahead));
            }
            private set
            {
                this.actualLookahead = Math.Min(this.MaximumLookahead, Math.Max(this.MinimumLookahead, value));
            }
        }

        /// <summary>Number of blocks downloaded by the puller.</summary>
        public int DownloadedCount
        {
            get { return this.DownloadedBlocks.Count; }
        }

        /// <summary>Maximum number of bytes used by unconsumed blocks that the puller is willing to maintain.</summary>
        public int MaxBufferedSize
        {
            get;
            set;
        }
        
        /// <summary>Current number of bytes that unconsumed blocks are occupying.</summary>
        private long currentSize;

        private List<int> downloadedCounts = new List<int>();

        /// <summary>Points to a block header that identifies the current position of the puller in the chain.</summary>
        private ChainedBlock location;
        /// <summary>Points to a block header that identifies the current position of the puller in the chain.</summary>
        public ChainedBlock Location
        {
            get { return this.location; }
        }

        /// <summary>Event that signals when a downloaded block is consumed.</summary>
        private AutoResetEvent consumed = new AutoResetEvent(false);
        /// <summary>Event that signals when a new block is pushed to the list of downloaded blocks.</summary>
        private AutoResetEvent pushed = new AutoResetEvent(false);

        private ChainedBlock lookaheadLocation;
        public ChainedBlock LookaheadLocation
        {
            get
            {
                return this.lookaheadLocation;
            }
        }

        /// <summary>?????????????????</summary>
        public decimal MedianDownloadCount
        {
            get
            {
                if (this.downloadedCounts.Count == 0)
                    return decimal.One;
                return (decimal)GetMedian(this.downloadedCounts);
            }
        }

        /// <summary>If true, the puller is a bottleneck.</summary>
        public bool IsStalling
        {
            get;
            internal set;
        }

        /// <summary>If true, the puller consumer is a bottleneck.</summary>
        public bool IsFull
        {
            get;
            internal set;
        }

        /// <inheritdoc />
        public void SetLocation(ChainedBlock tip)
        {
            Guard.NotNull(tip, nameof(tip));
            this.location = tip;
        }

        /// <inheritdoc />
        public void RequestOptions(TransactionOptions transactionOptions)
        {
            if (transactionOptions == TransactionOptions.Witness)
            {
                this.Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
                foreach (var node in this.Nodes.Select(n => n.Behaviors.Find<BlockPullerBehavior>()))
                {
                    if (!this.Requirements.Check(node.AttachedNode.PeerVersion))
                    {
                        node.ReleaseAll();
                    }
                }
            }
        }

        /// <inheritdoc />
        public Block NextBlock(CancellationToken cancellationToken)
        {
            this.downloadedCounts.Add(this.DownloadedBlocks.Count);
            if (this.lookaheadLocation == null)
            {
                AskBlocks();
                AskBlocks();
            }
            var block = NextBlockCore(cancellationToken);
            if (block == null)
            {
                //A reorg
                return null;
            }
            if ((this.lookaheadLocation.Height - this.location.Height) <= this.ActualLookahead)
            {
                CalculateLookahead();
                AskBlocks();
            }
            return block;
        }

        /// <summary>
        /// Finds median for list of values.
        /// </summary>
        /// <param name="sourceNumbers">List of values to find median for.</param>
        /// <returns>Median of the input values.</returns>
        private static decimal GetMedian(List<int> sourceNumbers)
        {
            // Framework 2.0 version of this method. There is an easier way in F4.
            if (sourceNumbers == null || sourceNumbers.Count == 0)
                throw new Exception("Median of empty array not defined.");

            // Make sure the list is sorted, but use a new array.
            sourceNumbers.Sort();

            // Get the median.
            int size = sourceNumbers.Count;
            int mid = size / 2;
            decimal median = (size % 2 != 0) ? (decimal)sourceNumbers[mid] : ((decimal)sourceNumbers[mid] + (decimal)sourceNumbers[mid - 1]) / 2;
            return median;
        }

        // If blocks ActualLookahead is 8:
        // If the number of downloaded block reach 2 or below, then ActualLookahead will be multiplied by 1.1.
        // If it reach 14 or above, it will be divided by 1.1.
        private void CalculateLookahead()
        {
            decimal medianDownloads = GetMedian(this.downloadedCounts);
            this.downloadedCounts.Clear();
            var expectedDownload = this.ActualLookahead * 1.1m;
            decimal tolerance = 0.05m;
            var margin = expectedDownload * tolerance;
            if (medianDownloads <= expectedDownload - margin)
                this.ActualLookahead = (int)Math.Max(this.ActualLookahead * 1.1m, this.ActualLookahead + 1);
            else if (medianDownloads >= expectedDownload + margin)
                this.ActualLookahead = (int)Math.Min(this.ActualLookahead / 1.1m, this.ActualLookahead - 1);
        }

        /// <inheritdoc />
        public Block TryGetLookahead(int count)
        {
            var chainedBlock = this.Chain.GetBlock(this.location.Height + 1 + count);
            if (chainedBlock == null)
                return null;
            var block = this.DownloadedBlocks.TryGet(chainedBlock.HashBlock);
            if (block == null)
                return null;
            return block.Block;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Making this method public allows to push blocks directly to the downloader, 
        /// which is used for testing and mining.
        /// </remarks>
        public override void PushBlock(int length, Block block, CancellationToken token)
        {
            var hash = block.Header.GetHash();
            var header = this.Chain.GetBlock(hash);
            while (this.currentSize + length >= this.MaxBufferedSize && header.Height != this.location.Height + 1)
            {
                this.IsFull = true;
                this.consumed.WaitOne(1000);
                token.ThrowIfCancellationRequested();
            }
            this.IsFull = false;
            this.DownloadedBlocks.TryAdd(hash, new DownloadedBlock { Block = block, Length = length });
            this.currentSize += length;
            this.pushed.Set();
        }

        /// <summary>
        /// Prepares and invokes download tasks from peer nodes for blocks the node is missing.
        /// </summary>
        private void AskBlocks()
        {
            if (this.location == null)
                throw new InvalidOperationException("SetLocation should have been called");
            if (this.lookaheadLocation == null && !this.Chain.Contains(this.location))
                return;
            if (this.lookaheadLocation != null && !this.Chain.Contains(this.lookaheadLocation))
                this.lookaheadLocation = null;

            ChainedBlock[] downloadRequests = null;

            ChainedBlock lookaheadBlock = this.lookaheadLocation ?? this.location;
            ChainedBlock nextLookaheadBlock = this.Chain.GetBlock(Math.Min(lookaheadBlock.Height + this.ActualLookahead, this.Chain.Height));
            if (nextLookaheadBlock == null)
                return;
            ChainedBlock fork = nextLookaheadBlock.FindFork(lookaheadBlock);

            this.lookaheadLocation = nextLookaheadBlock;

            downloadRequests = new ChainedBlock[nextLookaheadBlock.Height - fork.Height];
            if (downloadRequests.Length == 0)
                return;
            for (int i = 0; i < downloadRequests.Length; i++)
            {
                downloadRequests[downloadRequests.Length - i - 1] = nextLookaheadBlock;
                nextLookaheadBlock = nextLookaheadBlock.Previous;
            }

            AskBlocks(downloadRequests);
        }

        /// <summary>
        /// Number of milliseconds to wait for a block in each iteration in NextBlockCore method.
        /// The array is processed in circular manner.
        /// </summary>
        private static int[] waitTime = new[] { 1, 10, 20, 40, 100, 1000 };

        /// <summary>
        /// Waits for a next block to be available (downloaded).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to allow the caller to cancel waiting for the next block.</param>
        /// <returns>Next block or null if a reorganization happened on the chain.</returns>
        private Block NextBlockCore(CancellationToken cancellationToken)
        {
            int i = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var header = this.Chain.GetBlock(this.location.Height + 1);
                DownloadedBlock block;
                if (header != null && this.DownloadedBlocks.TryRemove(header.HashBlock, out block))
                {
                    if (header.Previous.HashBlock != this.location.HashBlock)
                    {
                        //A reorg
                        return null;
                    }
                    this.IsStalling = false;
                    this.location = header;
                    Interlocked.Add(ref this.currentSize, -block.Length);
                    this.consumed.Set();
                    return block.Block;
                }
                else
                {
                    if (header == null)
                    {
                        if (!this.Chain.Contains(this.location.HashBlock))
                        {
                            //A reorg
                            return null;
                        }
                    }
                    else
                    {
                        if (!IsDownloading(header.HashBlock))
                            AskBlocks(new ChainedBlock[] { header });
                        OnStalling(header);
                        this.IsStalling = true;
                    }
                    WaitHandle.WaitAny(new[] { this.pushed, cancellationToken.WaitHandle }, waitTime[i]);
                }
                i = i == waitTime.Length - 1 ? 0 : i + 1;
            }
        }
    }
}
