using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>Base class for each block store step.</summary>
    internal abstract class BlockStoreLoopStep
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        protected BlockStoreLoopStep(BlockStoreLoop blockStoreLoop, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(blockStoreLoop, nameof(blockStoreLoop));

            this.loggerFactory = loggerFactory;
            this.BlockStoreLoop = blockStoreLoop;
        }

        internal BlockStoreLoop BlockStoreLoop;

        internal abstract Task<StepResult> ExecuteAsync(ChainedBlock nextChainedBlock, CancellationToken cancellationToken, bool disposeMode);
    }

    /// <summary>
    /// The result that is returned from executing each loop step.
    /// </summary>
    public enum StepResult
    {
        /// <summary>Continue execution of the loop.</summary>
        Continue,

        /// <summary>Execute the next line of code in the loop.</summary>
        Next,

        /// <summary>Break out of the loop.</summary>
        Stop,
    }
}
