using System.Collections.Generic;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    /// <summary>
    /// Feedback specific to when a partial block validation succeeded.
    /// <para>
    /// Specifically, it determines whether or not full validation is required and the
    /// next set of headers to validate.
    /// </para>
    /// </summary>
    public sealed class PartialValidationSucceededResult
    {
        /// <summary>List of chained header blocks with block data that should be partially validated next. Or <c>null</c> if none should be validated.</summary>
        public List<ChainedHeaderBlock> ChainedHeaderBlocksToValidate { get; set; }

        /// <summary><c>true</c> in case we want to switch our consensus tip to <paramref name="chainedHeader"/>.</summary>
        public bool FullValidationRequired { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $",{nameof(this.ChainedHeaderBlocksToValidate.Count)}={this.ChainedHeaderBlocksToValidate.Count}, {nameof(this.FullValidationRequired)}={this.FullValidationRequired}";
        }
    }
}