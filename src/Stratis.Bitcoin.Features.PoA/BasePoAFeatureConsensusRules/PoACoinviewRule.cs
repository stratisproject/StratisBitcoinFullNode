using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules
{
    public class PoACoinviewRule : CoinViewRule
    {
        private PoANetwork network;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            this.network = this.Parent.Network as PoANetwork;
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            Money reward = Money.Zero;

            if (height == this.network.Consensus.PremineHeight)
                reward = this.network.Consensus.PremineReward;

            if (block.Transactions[0].TotalOut > fees + reward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }
        }

        /// <inheritdoc/>
        public override Money GetProofOfWorkReward(int height)
        {
            if (height == this.network.Consensus.PremineHeight)
                return this.network.Consensus.PremineReward;

            return 0;
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
    }
}
