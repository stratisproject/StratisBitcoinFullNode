using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    /// <summary>
    /// Rules that provide easy access to the <see cref="CoinView"/> which is the store for a PoW system.
    /// </summary>
    public abstract class UtxoStoreConsensusRule : ConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PowConsensusRules PowParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PowParent = this.Parent as PowConsensusRules;

            Guard.NotNull(this.PowParent, nameof(this.PowParent));
        }
    }

    /// <summary>
    /// Rules that provide easy access to the <see cref="IStakeChain"/> which is the store for a PoS system.
    /// </summary>
    public abstract class StakeStoreConsensusRule : ConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRules PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRules;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }
    }
}