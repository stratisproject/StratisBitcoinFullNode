using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public sealed class PowCoinviewRule : CoinViewRule
    {
        /// <inheritdoc/>
        public override Money GetProofOfWorkReward(int height)
        {
            int halvings = height / this.ConsensusParams.SubsidyHalvingInterval;
            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.PowConsensusOptions.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;
            return subsidy;
        }
    }
}