using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.ValidationResults;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Validators
{
    /// <summary>
    /// A callback that is invoked when <see cref="IPartialValidator.StartPartialValidation"/> completes validation of a block.
    /// </summary>
    /// <param name="validationResult">Result of the validation including information about banning if necessary.</param>
    public delegate Task OnPartialValidationCompletedAsyncCallback(PartialValidationResult validationResult);

    public interface IHeaderValidator
    {
        /// <summary>
        /// Validates a block header.
        /// </summary>
        /// <param name="chainedHeader">The chained header to be validated.</param>
        void ValidateHeader(ChainedHeader chainedHeader);
    }

    public interface IPartialValidator : IDisposable
    {
        /// <summary>
        /// Schedules a block for background partial validation.
        /// <para>
        /// Partial validation doesn't involve change to the underlying store like rewinding or updating the database.
        /// </para>
        /// </summary>
        /// <param name="chainedHeaderBlock">The block to validate.</param>
        /// <param name="onPartialValidationCompletedAsyncCallback">A callback that is called when validation is complete.</param>
        void StartPartialValidation(ChainedHeaderBlock chainedHeaderBlock, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback);

        /// <summary>
        /// Executes the partial validation rule set on a block.
        /// <para>
        /// Partial validation doesn't involve change to the underlying store like rewinding or updating the database.
        /// </para>
        /// </summary>
        /// <param name="block">The block to validate.</param>
        /// <param name="chainedHeader">The chained header to included in validation.</param>
        Task<PartialValidationResult> ValidateAsync(Block block, ChainedHeader chainedHeader);
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

    /// <inheritdoc />
    public class HeaderValidator : IHeaderValidator
    {
        private readonly IConsensusRuleEngine consensusRules;
        private readonly ILogger logger;

        public HeaderValidator(IConsensusRuleEngine consensusRules, ILoggerFactory loggerFactory)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void ValidateHeader(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            var validationContext = new ValidationContext { ChainTipToExtand = chainedHeader };

            this.consensusRules.HeaderValidation(validationContext);

            this.logger.LogTrace("(-)");
        }
    }

    /// <inheritdoc />
    public class IntegrityValidator : IIntegrityValidator
    {
        private readonly IConsensusRuleEngine consensusRules;
        private readonly ILogger logger;

        public IntegrityValidator(IConsensusRuleEngine consensusRules, ILoggerFactory loggerFactory)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void VerifyBlockIntegrity(Block block, ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader);

            var validationContext = new ValidationContext { Block = block, ChainTipToExtand = chainedHeader };

            this.consensusRules.IntegrityValidation(validationContext);

            this.logger.LogTrace("(-)");
        }
    }

    /// <inheritdoc />
    public class PartialValidator : IPartialValidator
    {
        private readonly IConsensusRuleEngine consensusRules;
        private readonly AsyncQueue<PartialValidationItem> asyncQueue;
        private readonly ILogger logger;

        public PartialValidator(IConsensusRuleEngine consensusRules, ILoggerFactory loggerFactory)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.asyncQueue = new AsyncQueue<PartialValidationItem>(this.OnEnqueueAsync);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.asyncQueue.Dispose();
        }

        private async Task OnEnqueueAsync(PartialValidationItem item, CancellationToken cancellationtoken)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(item), item);

            var validationContext = new ValidationContext { Block = item.ChainedHeaderBlock.Block, ChainTipToExtand = item.ChainedHeaderBlock.ChainedHeader };

            await this.consensusRules.PartialValidationAsync(validationContext).ConfigureAwait(false);

            var partialValidationResult = new PartialValidationResult
            {
                ChainedHeaderBlock = item.ChainedHeaderBlock,
                BanDurationSeconds = validationContext.BanDurationSeconds,
                BanReason = validationContext.Error != null ? $"Invalid block received: {validationContext.Error.Message}" : string.Empty,
                Succeeded = validationContext.Error == null
            };

            try
            {
                await item.PartialValidationCompletedAsyncCallback(partialValidationResult).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                this.logger.LogCritical("Partial validation callback threw an exception: {0}.", exception.ToString());
                throw;
            }

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<PartialValidationResult> ValidateAsync(Block block, ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block.GetHash());

            var chainedHeaderBlock = new ChainedHeaderBlock(block, chainedHeader);
            var validationContext = new ValidationContext { Block = block, ChainTipToExtand = chainedHeader };

            await this.consensusRules.PartialValidationAsync(validationContext).ConfigureAwait(false);

            var partialValidationResult = new PartialValidationResult
            {
                BanDurationSeconds = validationContext.BanDurationSeconds,
                BanReason = validationContext.Error != null ? $"Invalid block received: {validationContext.Error.Message}" : string.Empty,
                ChainedHeaderBlock = validationContext.Error == null ? chainedHeaderBlock : null,
                Error = validationContext.Error,
                Succeeded = validationContext.Error == null
            };

            this.logger.LogTrace("(-):{0}", partialValidationResult, partialValidationResult);
            return partialValidationResult;
        }

        /// <summary>
        /// Hold information related to partial validation.
        /// </summary>
        public class PartialValidationItem
        {
            /// <summary>The block and the header to be partially validated.</summary>
            public ChainedHeaderBlock ChainedHeaderBlock { get; set; }

            /// <summary>After validation a call back will be invoked asynchronously.</summary>
            public OnPartialValidationCompletedAsyncCallback PartialValidationCompletedAsyncCallback { get; set; }

            /// <inheritdoc />
            public override string ToString()
            {
                return $"{nameof(this.ChainedHeaderBlock)}={this.ChainedHeaderBlock}";
            }
        }
    }
}