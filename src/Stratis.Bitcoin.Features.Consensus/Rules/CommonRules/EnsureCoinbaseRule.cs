using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class EnsureCoinbaseRule : ConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

            // First transaction must be coinbase, the rest must not be
            if ((block.Transactions.Count == 0) || !block.Transactions[0].IsCoinBase)
            {
                this.Logger.LogTrace("(-)[NO_COINBASE]");
                ConsensusErrors.BadCoinbaseMissing.Throw();
            }

            for (int i = 1; i < block.Transactions.Count; i++)
            {
                if (block.Transactions[i].IsCoinBase)
                {
                    this.Logger.LogTrace("(-)[MULTIPLE_COINBASE]");
                    ConsensusErrors.BadMultipleCoinbase.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}