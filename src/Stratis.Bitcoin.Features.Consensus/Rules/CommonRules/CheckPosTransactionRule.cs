using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{    /// <summary>
     /// Validate a PoS transaction.
     /// </summary>
    public class CheckPosTransactionRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErros.BadTransactionEmptyOutput">The transaction output is empty.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.Block;

            // Check transactions
            foreach (Transaction tx in block.Transactions)
                this.CheckTransaction(tx);

            return Task.CompletedTask;
        }

        public virtual void CheckTransaction(Transaction transaction)
        {
            foreach (TxOut txout in transaction.Outputs)
            {
                if (txout.IsEmpty && !transaction.IsCoinBase && !transaction.IsCoinStake)
                {
                    this.Logger.LogTrace("(-)[USER_TXOUT_EMPTY]");
                    ConsensusErrors.BadTransactionEmptyOutput.Throw();
                }
            }
        }
    }
}