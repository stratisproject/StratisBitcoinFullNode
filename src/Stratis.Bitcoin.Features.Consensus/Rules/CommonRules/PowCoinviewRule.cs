using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <inheritdoc />
    [ExecutionRule]
    public sealed class PowCoinviewRule : CoinViewRule
    {
        /// <summary>Consensus parameters.</summary>
        private NBitcoin.Consensus consensusParams;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Logger.LogTrace("()");

            base.Initialize();

            this.consensusParams = this.Parent.Network.Consensus;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            this.Logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override Money GetProofOfWorkReward(int height)
        {
            int halvings = height / this.consensusParams.SubsidyHalvingInterval;

            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.PowConsensusOptions.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;

            return subsidy;
        }

        /// <inheritdoc/>
        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            base.CheckCoinbaseMaturity(coins, spendHeight);
        }

        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            base.UpdateUTXOSet(context, transaction);
        }

        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            return base.RunAsync(context);
        }
    }
}