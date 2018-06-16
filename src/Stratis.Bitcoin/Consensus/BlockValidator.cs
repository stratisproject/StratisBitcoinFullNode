using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus
{
    // TODO: this might be broken to smaller interfaces that IBlockValidator will inherit from.
    /// <summary>Validates <see cref="ChainedHeader"/> instances.</summary>
    public interface IBlockValidator
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

        /// <summary>
        /// Partial validation of a block, this will not changes any state in the consensus store when validating a block.
        /// </summary>
        /// <param name="chainedHeaderBlock">The block to validate.</param>
        /// <param name="onPartialValidationCompletedCallback">A callback that is called when validation is complete.</param>
        void StartPartialValidation(ChainedHeaderBlock chainedHeaderBlock, OnPartialValidationCompletedCallback onPartialValidationCompletedCallback);
    }

    /// <summary>
    /// A callback that is invoked when <see cref="IBlockValidator.StartPartialValidation"/> completes validation of a block.
    /// </summary>
    /// <param name="chainedHeaderBlock">The block and its chained header.</param>
    /// <param name="success">An indicator whether validation succeeded.</param>
    public delegate void OnPartialValidationCompletedCallback(ChainedHeaderBlock chainedHeaderBlock, bool success);
}
