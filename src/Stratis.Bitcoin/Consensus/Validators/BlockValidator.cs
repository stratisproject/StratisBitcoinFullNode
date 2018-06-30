using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.Validators
{
    // TODO: this might be broken to smaller interfaces that IBlockValidator will inherit from.
    /// <summary>Validates <see cref="ChainedHeader"/> instances.</summary>
    public interface IBlockValidator : IHeaderValidator, IPartialValidation
    {
    }

    /// <summary>
    /// A callback that is invoked when <see cref="IBlockValidator.StartPartialValidation"/> completes validation of a block.
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

    public interface IPartialValidation
    {
        /// <summary>
        /// Partial validation of a block, this will not changes any state in the consensus store when validating a block.
        /// </summary>
        /// <param name="chainedHeaderBlock">The block to validate.</param>
        /// <param name="onPartialValidationCompletedAsyncCallback">A callback that is called when validation is complete.</param>
        void StartPartialValidation(ChainedHeaderBlock chainedHeaderBlock, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback);
    }

    // <inheritdoc />
    public class HeaderValidator : IHeaderValidator
    {
        // <inheritdoc />
        public void ValidateHeader(ChainedHeader chainedHeader)
        {
            throw new System.NotImplementedException();
        }

        // <inheritdoc />
        public void VerifyBlockIntegrity(Block block, ChainedHeader chainedHeader)
        {
            throw new System.NotImplementedException();
        }
    }

    // <inheritdoc />
    public class PartialValidation : IPartialValidation
    {
        // <inheritdoc />
        public void StartPartialValidation(ChainedHeaderBlock chainedHeaderBlock, OnPartialValidationCompletedAsyncCallback onPartialValidationCompletedAsyncCallback)
        {
            throw new System.NotImplementedException();
        }
    }
}
