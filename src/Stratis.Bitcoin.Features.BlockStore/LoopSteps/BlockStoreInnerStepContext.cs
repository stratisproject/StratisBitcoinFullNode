using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore.LoopSteps
{
    /// <summary>
    /// Context for the inner steps, <see cref="BlockStoreInnerStepFindBlocks"/> and <see cref="BlockStoreInnerStepReadBlocks"/>.
    /// <para>
    /// The context also initializes the inner step <see cref="InnerSteps"/>.
    /// </para>
    /// </summary>
    public sealed class BlockStoreInnerStepContext
    {
        /// <summary>Number of milliseconds to wait after each failed attempt to get a block from the block puller.</summary>
        internal const int StallDelayMs = 100;

        /// <summary><see cref="DownloadStack"/> is flushed to the disk if more than this amount of milliseconds passed since the last flush was made.</summary>
        internal const int MaxDownloadStackFlushTimeMs = 20 * 1000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Provider of time functions.</summary>
        internal readonly IDateTimeProvider DateTimeProvider;

        /// <summary>Number of attempts to obtain a block from the block puller before giving up and requesting the block again.</summary>
        /// <remarks>If the threshold is reached, it is increased to allow more attempts next time.</remarks>
        internal int StallCountThreshold = 1800;

        /// <summary>Timestamp of the last flush of <see cref="DownloadStack"/> to the disk.</summary>
        internal DateTime LastDownloadStackFlushTime;

        public BlockStoreInnerStepContext(CancellationToken cancellationToken, BlockStoreLoop blockStoreLoop, ChainedBlock nextChainedBlock, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
        {
            Guard.NotNull(blockStoreLoop, nameof(blockStoreLoop));
            Guard.NotNull(nextChainedBlock, nameof(nextChainedBlock));

            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);

            this.BlockStoreLoop = blockStoreLoop;
            this.CancellationToken = cancellationToken;
            this.DateTimeProvider = dateTimeProvider;
            this.DownloadStack = new Queue<ChainedBlock>();
            this.InnerSteps = new List<BlockStoreInnerStep> { new BlockStoreInnerStepFindBlocks(this.loggerFactory), new BlockStoreInnerStepReadBlocks(this.loggerFactory) };
            this.InsertBlockSize = 0;
            this.LastDownloadStackFlushTime = this.DateTimeProvider.GetUtcNow();
            this.NextChainedBlock = nextChainedBlock;
            this.StallCount = 0;
            this.Store = new List<BlockPair>();
        }

        /// <summary>The number of blocks pushed to repository. This gets reset when the next
        /// set of blocks are asked from the puller</summary>
        public int BlocksPushedCount { get; set; }

        /// <summary>A queue of blocks to be downloaded.</summary>
        public Queue<ChainedBlock> DownloadStack { get; private set; }

        /// <summary>The maximum number of blocks to ask for.</summary>
        public const int DownloadStackThreshold = 100;

        /// <summary>The maximum number of blocks to read from the puller before asking for blocks again.</summary>
        public const int DownloadStackPushThreshold = 50;

        public BlockStoreLoop BlockStoreLoop { get; private set; }

        /// <summary>The chained block the inner step starts on.</summary>
        public ChainedBlock InputChainedBlock { get; private set; }

        public ChainedBlock NextChainedBlock { get; private set; }

        /// <summary>The routine (list of inner steps) the DownloadBlockStep executes.</summary>
        public List<BlockStoreInnerStep> InnerSteps { get; private set; }

        public CancellationToken CancellationToken;

        /// <summary>
        /// A store of blocks that will be pushed to the repository once the <see cref="BlockStoreLoop.MaxInsertBlockSize"/> has been reached.
        /// </summary>
        public List<BlockPair> Store;

        public int InsertBlockSize;
        public int StallCount;

        /// <summary> Sets the next chained block to process.</summary>
        internal void GetNextBlock()
        {
            this.logger.LogTrace("()");

            this.InputChainedBlock = this.NextChainedBlock;
            this.NextChainedBlock = this.BlockStoreLoop.Chain.GetBlock(this.InputChainedBlock.Height + 1);

            this.logger.LogTrace("(-):{0}='{1}'", nameof(this.NextChainedBlock), this.NextChainedBlock);
        }

        /// <summary> Removes BlockStoreInnerStepFindBlocks from the routine.</summary>
        internal void StopFindingBlocks()
        {
            this.logger.LogTrace("()");

            this.InnerSteps.Remove(this.InnerSteps.OfType<BlockStoreInnerStepFindBlocks>().First());

            this.logger.LogTrace("(-)");
        }
    }

    /// <summary>Abstract class that all DownloadBlockSteps implement</summary>
    public abstract class BlockStoreInnerStep
    {
        public abstract Task<InnerStepResult> ExecuteAsync(BlockStoreInnerStepContext context);
    }
}