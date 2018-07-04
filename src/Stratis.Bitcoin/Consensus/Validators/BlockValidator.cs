using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Validators
{
    /// <summary>
    /// A callback that is invoked when <see cref="IPartialValidation.StartPartialValidation"/> completes validation of a block.
    /// </summary>
    /// <param name="validationResult">Result of the validation including information about banning if necessary.</param>
    public delegate Task OnPartialValidationCompletedAsyncCallback(PartialValidationResult validationResult);

    public interface IHeaderValidator
    {
        /// <summary>
        /// Validation of a header that was seen for the first time.
        /// </summary>
        /// <param name="chainedHeader">The chained header to be validated.</param>
        void ValidateHeader(ChainedHeader chainedHeader);
    }

    public interface IPartialValidation
    {
        /// <summary>
        /// Partial validation of a block, this will not changes any state in the consensus store when validating a block.
        /// </summary>
        /// <param name="chainedHeaderBlock">The block to validate.</param>
        /// <param name="onPartialValidationCompletedAsyncCallback">A callback that is called when validation is complete.</param>
        void StartPartialValidation(ChainedHeaderBlock chainedHeaderBlock, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback);
    }

    public interface IIntegrityValidator
    {
        /// <summary>
        /// Verifies that the block data corresponds to the chain header.
        /// </summary>
        /// <remarks>  
        /// This validation represents minimal required validation for every block that we download.
        /// It should be performed even if the block is behind last checkpoint or part of assume valid chain.
        /// TODO specify what exceptions are thrown (add throws xmldoc)
        /// </remarks>
        /// <param name="block">The block that is going to be validated.</param>
        /// <param name="chainedHeader">The chained header of the block that will be validated.</param>
        void VerifyBlockIntegrity(Block block, ChainedHeader chainedHeader);
    }

    // <inheritdoc />
    public class HeaderValidator : IHeaderValidator
    {
        // <inheritdoc />
        public void ValidateHeader(ChainedHeader chainedHeader)
        {
            // TODO: add rule attributes for header validation
        }
    }

    // <inheritdoc />
    public class IntegrityValidator : IIntegrityValidator
    {
        // <inheritdoc />
        public void VerifyBlockIntegrity(Block block, ChainedHeader chainedHeader)
        {
            // TODO: add rule attributes for partial validation
        }
    }

    // <inheritdoc />
    public class PartialValidation : IPartialValidation
    {
        private readonly IConsensusRules consensusRules;
        private readonly AsyncQueue<PartialValidationItem> asyncQueue;
        private readonly ILogger logger;

        public PartialValidation(IConsensusRules consensusRules, ILoggerFactory loggerFactory)
        {
            this.consensusRules = consensusRules;
            this.asyncQueue = new AsyncQueue<PartialValidationItem>(this.OnEnqueueAsync);

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        private async Task OnEnqueueAsync(PartialValidationItem item, CancellationToken cancellationtoken)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(item), item);

            var validationContext = new ValidationContext {Block = item.ChainedHeaderBlock.Block};

            await this.consensusRules.PartialValidationAsync(new ValidationContext { Block = item.ChainedHeaderBlock.Block }, null);

            var partialValidationResult = new PartialValidationResult
            {
                ChainedHeaderBlock = item.ChainedHeaderBlock,
                BanDurationSeconds = validationContext.BanDurationSeconds,
                Succeeded = validationContext.Error != null
            };

            await item.PartialValidationCompletedAsyncCallback(partialValidationResult);

            this.logger.LogTrace("(-):{0}", partialValidationResult);
        }

        // <inheritdoc />
        public void StartPartialValidation(ChainedHeaderBlock chainedHeaderBlock, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeaderBlock), chainedHeaderBlock);

            this.asyncQueue.Enqueue(new PartialValidationItem()
            {
                ChainedHeaderBlock = chainedHeaderBlock,
                PartialValidationCompletedAsyncCallback = onPartialValidationCompletedAsyncCallback
            });

            this.logger.LogTrace("(-)");
        }

        public class PartialValidationItem
        {
            public ChainedHeaderBlock ChainedHeaderBlock { get; set; }

            public OnPartialValidationCompletedAsyncCallback PartialValidationCompletedAsyncCallback { get; set; }
        }
    }
}