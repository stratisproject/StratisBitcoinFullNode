using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Consensus.ValidationResults
{
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