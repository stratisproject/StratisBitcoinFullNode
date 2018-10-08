using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
    public class PoACoinviewRule : CoinViewRule
    {
        // TODO POA check this rule properly later, it's a dummy implementation needed for miner

        /// <inheritdoc/>
        protected override bool IsProtocolTransaction(Transaction transaction)
        {
            return transaction.IsCoinBase;
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            if (block.Transactions[0].TotalOut > fees)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }
        }

        /// <inheritdoc/>
        public override Money GetProofOfWorkReward(int height)
        {
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
