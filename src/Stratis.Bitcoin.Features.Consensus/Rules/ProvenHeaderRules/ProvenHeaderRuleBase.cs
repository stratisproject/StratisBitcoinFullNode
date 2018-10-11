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

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
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
            if (this.PosParent.Network.Consensus.Options is PosConsensusOptions options)
            {
                return height >= options.ProvenHeadersActivationHeight;
            }

            return false;
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
    }
}
