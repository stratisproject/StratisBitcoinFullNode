using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Block puller used for fast sync during initial block download (IBD). 
    /// </summary>
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
        /// Waits for a next block to be available (downloaded) and returns it to the consumer.
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
    /// Puller that is used for fast sync during initial block download (IBD). It implements a strategy of downloading 
    /// multiple blocks from multiple peers at once, so that IBD is fast enough, but does not consume too many resources.
    /// </summary>
    /// <remarks>
    /// The node is aware of the longest chain of block headers, which is stored in this.Chain. This is the chain the puller 
    /// needs to download. The algorithm works with following values: ActualLookahead, MinimumLookahead, MaximumLookahead,
    /// location, and lookaheadLocation.
    /// <para>
    /// ActualLookahead is a number of blocks that we wish to download at the same time, it varies between MinimumLookahead 
    /// and MaximumLookahead depending on the consumer's speed and node download speed. Calling AskBlocks() increases 
    /// lookaheadLocation by ActualLookahead. Here is a visualization of the block chain and how the puller sees it:
    /// </para>
    /// <para>
    /// -------A------B-------------C-----------D---------E--------
    /// </para>
    /// <para>
    /// Each '-' represents a block and letters 'A' to 'E' represent blocks with important positions in the chain from 
    /// the puller's perspective. The puller can be understood as a producer of the blocks (by requesting them from the peers 
    /// and downloading them) for the component that uses the puller that consumes the blocks (e.g. validating them).
    /// </para>
    /// <para>
    /// A is a position of a block that we call location. Blocks in front of A were already downloaded and consumed.
    /// </para>
    /// <para>
    /// B is a position of a block A + MinimumLookahead, and E is a position of block A + MaximumLookahead. 
    /// Blocks between B and E are the blocks that the puller is currently interested in. Blocks after E are currently 
    /// not considered and will only be interesting later. The lower boundary B prevents the IBD to be too slow, 
    /// while the upper boundary E prevents the puller from using too many resources.
    /// </para>
    /// <para>
    /// Blocks between A and B are blocks that have been downloaded already, but the consumer did not consume them yet.
    /// </para>
    /// <para>
    /// C is a position of a block A + lookaheadLocation. Blocks between B and C are currently being requested by the puller, 
    /// some of them could be already being downloaded. The block puller makes sure that if lookaheadLocation &lt; ActualLookahead 
    /// then AskBlocks() is called. During the initialization, or when reorganisation happens, lookaheadLocation is zero/null 
    /// and AskBlocks() needs to be called two times.
    /// </para>
    /// <para>
    /// D is a position of a block A + ActualLookahead. ActualLookahead is a number of blocks that the puller wants 
    /// to be downloading simultaneously. If there is a gap between C and D it means that the puller wants to start 
    /// downloading these blocks.
    /// </para>
    /// <para>
    /// Blocks between D and E are currently those that the puller does not want to be downloading right now, 
    /// but should the ActualLookahead be adjusted, they can be requested in the near future.
    /// </para>
    /// </remarks>
    public class LookaheadBlockPuller : BlockPuller, ILookaheadBlockPuller
    {
        /// <summary>Maximal size of a block in bytes.</summary>
        private const int MaxBlockSize = 2000000;

        /// <summary>Number of milliseconds for a single waiting round for the next block in the <see cref="NextBlockCore"/> loop.</summary>
        private const int WaitNextBlockRoundTimeMs = 100;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Lower limit for ActualLookahead.</summary>
        public int MinimumLookahead { get; set; }

        /// <summary>Upper limit for ActualLookahead.</summary>
        public int MaximumLookahead { get; set; }

        /// <summary>Number of blocks the puller wants to be downloading at once.</summary>
        private int actualLookahead;
        /// <summary>Number of blocks the puller wants to be downloading at once.</summary>
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

        /// <summary>Maximum number of bytes used by unconsumed blocks that the puller is willing to maintain.</summary>
        public int MaxBufferedSize { get; set; }

        /// <summary>Current number of bytes that unconsumed blocks are occupying.</summary>
        private long currentSize;

        /// <summary>Lock object to protect access to <see cref="downloadedCounts"/>.</summary>
        private readonly object downloadedCountsLock = new object();

        /// <summary>Maintains the statistics of number of downloaded blocks. This is used for calculating new actualLookahead value.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="downloadedCountsLock"/>.</remarks>
        private List<int> downloadedCounts = new List<int>();

        /// <summary>Points to a block that was consumed last time. The next block returned by the puller to the consumer will be at location + 1.</summary>
        private ChainedBlock location;
        /// <summary>Points to a block that was consumed last time. The next block returned by the puller to the consumer will be at location + 1.</summary>
        public ChainedBlock Location { get { return this.location; } }

        /// <summary>Event that signals when a downloaded block is consumed.</summary>
        private AutoResetEvent consumed = new AutoResetEvent(false);
        /// <summary>Event that signals when a new block is pushed to the list of downloaded blocks.</summary>
        private AutoResetEvent pushed = new AutoResetEvent(false);

        /// <summary>Identifies the last block that is currently being requested/downloaded.</summary>
        private ChainedBlock lookaheadLocation;
        /// <summary>Identifies the last block that is currently being requested/downloaded.</summary>
        public ChainedBlock LookaheadLocation { get { return this.lookaheadLocation; } }

        /// <summary>Median of a list of past downloadedCounts values. This is used just for logging purposes.</summary>
        public decimal MedianDownloadCount
        {
            get
            {
                lock (this.downloadedCountsLock)
                {
                    if (this.downloadedCounts.Count == 0)
                        return decimal.One;
                    return (decimal)GetMedian(this.downloadedCounts);
                }
            }
        }

        /// <summary>If true, the puller is a bottleneck.</summary>
        public bool IsStalling { get; internal set; }

        /// <summary>If true, the puller consumer is a bottleneck.</summary>
        public bool IsFull { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a connection manager. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="connectionManager">Manager of information about the node's network connections.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public LookaheadBlockPuller(ConcurrentChain chain, IConnectionManager connectionManager, ILoggerFactory loggerFactory)
            : base(chain, connectionManager.ConnectedNodes, connectionManager.NodeSettings.ProtocolVersion, loggerFactory)
        {
            this.MaxBufferedSize = MaxBlockSize * 10;
            this.MinimumLookahead = 4;
            this.MaximumLookahead = 2000;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
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
            this.logger.LogTrace($"({nameof(transactionOptions)}:{transactionOptions})");

            if (transactionOptions == TransactionOptions.Witness)
            {
                this.Requirements.RequiredServices |= NodeServices.NODE_WITNESS;
                foreach (BlockPullerBehavior node in this.Nodes.Select(n => n.Behaviors.Find<BlockPullerBehavior>()))
                {
                    if (!this.Requirements.Check(node.AttachedNode.PeerVersion))
                    {
                        this.logger.LogDebug($"Peer {node.GetHashCode():x} does not meet requirements, releasing its tasks.");
                        node.ReleaseAll();
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public Block NextBlock(CancellationToken cancellationToken)
        {
            this.logger.LogTrace("()");

            lock (this.downloadedCountsLock)
            {
                this.downloadedCounts.Add(this.DownloadedBlocksCount);
            }

            if (this.lookaheadLocation == null)
            {
                this.logger.LogTrace("Lookahead location is not initialized.");

                // Calling twice is intentional here.
                // lookaheadLocation is null only during initialization
                // or when reorganisation happens. Calling this twice will 
                // make sure the initial work of the puller is away from 
                // the lower boundary.
                AskBlocks();
                AskBlocks();
            }

            Block block = NextBlockCore(cancellationToken);
            if (block != null)
            {
                if ((this.lookaheadLocation.Height - this.location.Height) <= this.ActualLookahead)
                {
                    this.logger.LogTrace($"Recalculating lookahead: Last request block height is {this.lookaheadLocation.Height}, last processed block height is {this.location.Height}, {nameof(this.ActualLookahead)} is {this.ActualLookahead}.");
                    CalculateLookahead();
                    AskBlocks();
                }
                else this.logger.LogTrace($"Lookahead needs no adjustment.");
            }
            else this.logger.LogTrace("Reorganization detected.");

            this.logger.LogTrace($"(-):'{block}'");
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

        /// <summary>
        /// Calculates a new value for this.ActualLookahead to keep it within reasonable range.
        /// <para>
        /// This ensures that the puller is requesting enough new blocks quickly enough to 
        /// keep with the demand, but at the same time not too quickly.
        /// </para>
        /// </summary>
        private void CalculateLookahead()
        {
            this.logger.LogTrace("()");

            decimal medianDownloads = 0;
            lock (this.downloadedCountsLock)
            {
                medianDownloads = GetMedian(this.downloadedCounts);
                this.downloadedCounts.Clear();
            }

            decimal expectedDownload = this.ActualLookahead * 1.1m;
            decimal tolerance = 0.05m;
            decimal margin = expectedDownload * tolerance;
            if (medianDownloads <= expectedDownload - margin)
                this.ActualLookahead = (int)Math.Max(this.ActualLookahead * 1.1m, this.ActualLookahead + 1);
            else if (medianDownloads >= expectedDownload + margin)
                this.ActualLookahead = (int)Math.Min(this.ActualLookahead / 1.1m, this.ActualLookahead - 1);

            this.logger.LogTrace($"(-):{nameof(this.ActualLookahead)}={this.ActualLookahead}");
        }

        /// <inheritdoc />
        public Block TryGetLookahead(int count)
        {
            this.logger.LogTrace($"({nameof(count)}:{count})");

            ChainedBlock chainedBlock = this.Chain.GetBlock(this.location.Height + 1 + count);
            if (chainedBlock == null)
            {
                this.logger.LogTrace("(-)[NOT_KNOWN]");
                return null;
            }

            DownloadedBlock block = GetDownloadedBlock(chainedBlock.HashBlock);
            if (block == null)
            {
                this.logger.LogTrace("(-)[NOT_AVAILABLE]");
                return null;
            }

            this.logger.LogTrace($"(-):'{block.Block}'");
            return block.Block;
        }

        /// <inheritdoc />
        /// <remarks>
        /// This method waits for the block puller to have empty space (see <see cref="MaxBufferedSize"/>)) for a new block.
        /// This happens if the consumer is not consuming the downloaded blocks quickly enough - relative to how quickly the blocks are downloaded.
        /// TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/277
        /// </remarks>
        public override void BlockPushed(uint256 blockHash, DownloadedBlock downloadedBlock, CancellationToken cancellationToken)
        {
            this.logger.LogTrace($"({nameof(blockHash)}:'{blockHash}')");

            ChainedBlock header = this.Chain.GetBlock(blockHash);
            // TODO: Race condition here (and also below) on this.currentSize. How about this.location.Height?
            // https://github.com/stratisproject/StratisBitcoinFullNode/issues/277
            while ((this.currentSize + downloadedBlock.Length >= this.MaxBufferedSize) && (header.Height != this.location.Height + 1))
            {
                this.logger.LogTrace($"Waiting for free space in the puller. {nameof(this.currentSize)}={this.currentSize} + {nameof(downloadedBlock)}.{nameof(downloadedBlock.Length)}={downloadedBlock.Length} = {this.currentSize + downloadedBlock.Length} >= {this.MaxBufferedSize}.");
                this.IsFull = true;
                this.consumed.WaitOne(1000);
                cancellationToken.ThrowIfCancellationRequested();
            }
            this.IsFull = false;
            this.currentSize += downloadedBlock.Length;
            this.pushed.Set();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Prepares and invokes download tasks from peer nodes for blocks the node is missing.
        /// </summary>
        /// <remarks>TODO: Comment is missing here about the details of the logic in this method.</remarks>
        private void AskBlocks()
        {
            this.logger.LogTrace("()");

            if (this.location == null)
                throw new InvalidOperationException("SetLocation should have been called");

            if (this.lookaheadLocation == null && !this.Chain.Contains(this.location))
            {
                this.logger.LogTrace("(-)[REORG]");
                return;
            }

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

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Waits for a next block to be available (downloaded).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to allow the caller to cancel waiting for the next block.</param>
        /// <returns>Next block or null if a reorganization happened on the chain.</returns>
        private Block NextBlockCore(CancellationToken cancellationToken)
        {
            this.logger.LogTrace("()");

            Block res = null;

            while (res == null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                this.logger.LogTrace($"Requesting block at height {this.location.Height + 1}.");
                ChainedBlock header = this.Chain.GetBlock(this.location.Height + 1);
                DownloadedBlock block;

                bool isDownloading = false;
                bool isReady = false;
                if (header != null) CheckBlockStatus(header.HashBlock, out isDownloading, out isReady);

                // If block has been downloaded and is ready to be consumed, then remove it from the list of downloaded blocks and consume it.
                if (isReady && TryRemoveDownloadedBlock(header.HashBlock, out block))
                {
                    if (header.Previous.HashBlock != this.location.HashBlock)
                    {
                        this.logger.LogTrace("Blockchain reorganization detected.");
                        break;
                    }
                    this.IsStalling = false;
                    this.location = header;
                    Interlocked.Add(ref this.currentSize, -block.Length);
                    this.consumed.Set();

                    res = block.Block;
                }
                else
                {
                    // Otherwise we either have reorg, or we don't know the next block.
                    if (header == null)
                    {
                        if (!this.Chain.Contains(this.location.HashBlock))
                        {
                            this.logger.LogTrace("Blockchain reorganization detected.");
                            break;
                        }

                        this.logger.LogTrace("Hash of the next block is not known.");
                    }
                    else
                    {
                        this.logger.LogTrace($"Block not available.");

                        // Or the block is still being downloaded or we need to ask for this block to be downloaded.
                        if (!isDownloading) AskBlocks(new ChainedBlock[] { header });

                        OnStalling(header);
                        this.IsStalling = true;
                    }

                    WaitHandle.WaitAny(new[] { this.pushed, cancellationToken.WaitHandle }, WaitNextBlockRoundTimeMs);
                }
            }

            this.logger.LogTrace($"(-):'{res}'");
            return res;
        }
    }
}
