using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Validate a PoS transaction.</summary>
    public class CheckPosTransactionRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErros.BadTransactionEmptyOutput">The transaction output is empty.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            Block block = context.ValidationContext.BlockToValidate;

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