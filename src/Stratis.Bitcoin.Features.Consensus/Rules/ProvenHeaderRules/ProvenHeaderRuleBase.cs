using System;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Base rule to be used by all proven header validation rules.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Consensus.Rules.HeaderValidationConsensusRule" />
    public abstract class ProvenHeaderRuleBase : HeaderValidationConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;

        protected int ProvenHeadersActivationHeight;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));

            if (this.PosParent.Network.Consensus.Options is PosConsensusOptions options)
            {
                this.ProvenHeadersActivationHeight = options.ProvenHeadersActivationHeight;
            }
        }

        /// <summary>
        /// Determines whether proven headers are activated based on the proven header activation height and applicable network.
        /// </summary>
        /// <param name="height">The height of the header.</param>
        /// <returns>
        /// <c>true</c> if proven header height is past the activation height for the corresponding network;
        /// otherwise, <c>false</c>.
        /// </returns>
        public bool IsProvenHeaderActivated(int height)
        {
            return height >= this.ProvenHeadersActivationHeight;
        }

        /// <summary>
        /// Determines whether header is a proven header.
        /// </summary>
        /// <param name="header">The block header.</param>
        /// <returns><c>true</c> if header is a <see cref="ProvenBlockHeader"/>.</returns>
        public bool IsProvenHeader(BlockHeader header)
        {
            return header is ProvenBlockHeader;
        }

        /// <inheritdoc/>
        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;

            if (context.SkipValidation || !this.IsProvenHeaderActivated(chainedHeader.Height))
                return;

            ProcessRule((PosRuleContext)context, chainedHeader, (ProvenBlockHeader)chainedHeader.Header);
        }

        /// <summary>
        /// Processes the rule.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="chainedHeader">The chained header to be validated.</param>
        /// <param name="header">The Proven Header to be validated.</param>
        protected abstract void ProcessRule(PosRuleContext context, ChainedHeader chainedHeader, ProvenBlockHeader header);
    }
}
