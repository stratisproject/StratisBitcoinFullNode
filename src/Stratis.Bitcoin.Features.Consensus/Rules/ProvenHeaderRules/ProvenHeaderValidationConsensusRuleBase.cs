using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Base rule to be used by all proven header validation rules. 
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Consensus.Rules.HeaderValidationConsensusRule" />
    public abstract class ProvenHeaderValidationConsensusRuleBase : HeaderValidationConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }

        /// <summary>
        /// Determines whether proven headers are activated based on the proven header activation height and applicable network.
        /// </summary>
        /// <param name="context">The rule context.</param>
        /// <returns>
        /// <c>true</c> if proven header height is past the activation height for the corresponding network;
        /// otherwise, <c>false</c>.
        /// </returns>
        public bool IsProvenHeaderActivated(RuleContext context)
        {
            if (this.PosParent.Network is StratisMain)
            {
                return context.ValidationContext.ChainedHeaderToValidate.Height >= PosConsensusOptions.ProvenHeadersActivationHeightMainnet;
            }

            if (this.PosParent.Network is StratisTest)
            {
                return context.ValidationContext.ChainedHeaderToValidate.Height >= PosConsensusOptions.ProvenHeadersActivationHeightTestnet;
            }

            return false;
        }

        /// <summary>
        /// Determines whether header is a proven header.
        /// </summary>
        /// <param name="context">The rule context.</param>
        /// <returns><c>true</c> if header is a <see cref="ProvenBlockHeader"/>.</returns>
        public bool IsProvenHeader(RuleContext context)
        {
            return context.ValidationContext.ChainedHeaderToValidate.Header is ProvenBlockHeader;
        }
    }
}
