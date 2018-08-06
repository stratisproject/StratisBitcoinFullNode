using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.ValidationResults
{
    /// <summary>
    /// Feedback specific to a partial block validation.
    /// </summary>
    public class PartialValidationResult : ValidationResult
    {
        public ChainedHeaderBlock ChainedHeaderBlock { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Succeeded)
                return base.ToString();

            return base.ToString() + $",{nameof(this.ChainedHeaderBlock)}={this.ChainedHeaderBlock}";
        }
    }
}