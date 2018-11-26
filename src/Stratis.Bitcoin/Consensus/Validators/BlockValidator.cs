using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Validators
{
    /// <summary>
    /// A callback that is invoked when <see cref="IPartialValidator.StartPartialValidation"/> completes validation of a block.
    /// </summary>
    /// <param name="validationContext">Result of the validation including information about banning if necessary.</param>
    public delegate Task OnPartialValidationCompletedAsyncCallback(ValidationContext validationContext);

    public interface IHeaderValidator
    {
        /// <summary>
        /// Validates a block header.
        /// </summary>
        /// <param name="chainedHeader">The chained header to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        ValidationContext ValidateHeader(ChainedHeader chainedHeader);
    }

    public interface IPartialValidator : IDisposable
    {
        /// <summary>
        /// Schedules a block for background partial validation.
        /// <para>
        /// Partial validation doesn't involve change to the underlying store like rewinding or updating the database.
        /// </para>
        /// </summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <param name="onPartialValidationCompletedAsyncCallback">A callback that is called when validation is complete.</param>
        void StartPartialValidation(ChainedHeader header, Block block, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback);

        /// <summary>
        /// Executes the partial validation rule set on a block.
        /// <para>
        /// Partial validation doesn't involve change to the underlying store like rewinding or updating the database.
        /// </para>
        /// </summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        Task<ValidationContext> ValidateAsync(ChainedHeader header, Block block);
    }

    public interface IFullValidator
    {
        /// <summary>
        /// Executes the full validation rule set on a block.
        /// <para>
        /// Full validation may involve changes to the underlying store like rewinding or updating the database.
        /// </para>
        /// </summary>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        Task<ValidationContext> ValidateAsync(ChainedHeader header, Block block);
    }

    public interface IIntegrityValidator
    {
        /// <summary>
        /// Verifies that the block data corresponds to the chain header.
        /// </summary>
        /// <remarks>
        /// This validation represents minimal required validation for every block that we download.
        /// It should be performed even if the block is behind last checkpoint or part of assume valid chain.
        /// </remarks>
        /// <param name="header">The chained header that is going to be validated.</param>
        /// <param name="block">The block that is going to be validated.</param>
        /// <returns>Context that contains validation result related information.</returns>
        ValidationContext VerifyBlockIntegrity(ChainedHeader header, Block block);
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
        public ValidationContext ValidateHeader(ChainedHeader chainedHeader)
        {
            ValidationContext result = this.consensusRules.HeaderValidation(chainedHeader);

            return result;
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
        public ValidationContext VerifyBlockIntegrity(ChainedHeader header, Block block)
        {
            ValidationContext result = this.consensusRules.IntegrityValidation(header, block);

            this.logger.LogTrace("(-):'{0}'", result);
            return result;
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
            ValidationContext result = await this.consensusRules.PartialValidationAsync(item.ChainedHeader, item.Block).ConfigureAwait(false);

            try
            {
                await item.PartialValidationCompletedAsyncCallback(result).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                this.logger.LogCritical("Partial validation callback threw an exception: {0}.", exception.ToString());
                throw;
            }
        }

        /// <inheritdoc />
        public void StartPartialValidation(ChainedHeader header, Block block, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback)
        {
            this.asyncQueue.Enqueue(new PartialValidationItem()
            {
                ChainedHeader = header,
                Block = block,
                PartialValidationCompletedAsyncCallback = onPartialValidationCompletedAsyncCallback
            });
        }

        /// <inheritdoc />
        public async Task<ValidationContext> ValidateAsync(ChainedHeader header, Block block)
        {
            ValidationContext result = await this.consensusRules.PartialValidationAsync(header, block).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Holds information related to partial validation.
        /// </summary>
        private class PartialValidationItem
        {
            /// <summary>The header to be partially validated.</summary>
            public ChainedHeader ChainedHeader { get; set; }

            /// <summary>The block to be partially validated.</summary>
            public Block Block { get; set; }

            /// <summary>After validation a call back will be invoked asynchronously.</summary>
            public OnPartialValidationCompletedAsyncCallback PartialValidationCompletedAsyncCallback { get; set; }

            /// <inheritdoc />
            public override string ToString()
            {
                return $"{nameof(this.ChainedHeader)}={this.ChainedHeader},{nameof(this.Block)}={this.Block}";
            }
        }
    }

    /// <inheritdoc />
    public class FullValidator : IFullValidator
    {
        private readonly IConsensusRuleEngine consensusRules;
        private readonly ILogger logger;

        public FullValidator(IConsensusRuleEngine consensusRules, ILoggerFactory loggerFactory)
        {
            this.consensusRules = consensusRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public async Task<ValidationContext> ValidateAsync(ChainedHeader header, Block block)
        {
            ValidationContext result = await this.consensusRules.FullValidationAsync(header, block).ConfigureAwait(false);

            return result;
        }
    }
}