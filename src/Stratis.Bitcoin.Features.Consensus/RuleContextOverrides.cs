using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus
{
    public abstract class UtxoRuleContext : RuleContext
    {
        protected UtxoRuleContext()
        {
        }

        protected UtxoRuleContext(ValidationContext validationContext, DateTimeOffset time)
            : base(validationContext, time)
        {
        }

        /// <summary>
        /// The UTXO that are representing the current validated block.
        /// </summary>
        public UnspentOutputSet UnspentOutputSet { get; set; }
    }

    /// <summary>
    /// A context that is used by the <see cref="IConsensusRuleEngine"/> for the PoS network type.
    /// </summary>
    public class PosRuleContext : UtxoRuleContext
    {
        internal PosRuleContext()
        {
        }

        public PosRuleContext(BlockStake blockStake)
        {
            this.BlockStake = blockStake;
        }

        public PosRuleContext(ValidationContext validationContext, DateTimeOffset time)
            : base(validationContext, time)
        {
        }

        public BlockStake BlockStake { get; set; }

        public Dictionary<TxIn, TxOut> CoinStakePrevOutputs { get; set; }

        public Money TotalCoinStakeValueIn { get; set; }

        public uint256 HashProofOfStake { get; set; }

        public uint256 TargetProofOfStake { get; set; }
    }

    /// <summary>
    /// A context that is used by the <see cref="IConsensusRuleEngine"/> for the PoW network type.
    /// </summary>
    public class PowRuleContext : UtxoRuleContext
    {
        internal PowRuleContext()
        {
        }

        public PowRuleContext(ValidationContext validationContext, DateTimeOffset time)
            : base(validationContext, time)
        {
        }
    }
}
