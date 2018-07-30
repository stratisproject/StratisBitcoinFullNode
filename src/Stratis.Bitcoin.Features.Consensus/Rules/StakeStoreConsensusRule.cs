using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// Rules that provide easy access to the <see cref="IStakeChain"/> which is the store for a PoS system.
    /// </summary>
    public abstract class StakeStoreConsensusRule : ConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }
    }
}