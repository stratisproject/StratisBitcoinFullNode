using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Context checks on a POS block.</summary>
    public class PosCoinstakeRule : PartialValidationConsensusRule
    {
        /// <summary>Allow access to the POS parent.</summary>
        protected PosConsensusRuleEngine PosParent;

        /// <inheritdoc />
        public override void Initialize()
        {
            this.PosParent = this.Parent as PosConsensusRuleEngine;

            Guard.NotNull(this.PosParent, nameof(this.PosParent));
        }

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadStakeBlock">The coinbase output (first transaction) is not empty.</exception>
        /// <exception cref="ConsensusErrors.BadStakeBlock">The second transaction is not a coinstake transaction.</exception>
        /// <exception cref="ConsensusErrors.BadMultipleCoinstake">There are multiple coinstake tranasctions in the block.</exception>
        /// <exception cref="ConsensusErrors.BlockTimeBeforeTrx">The block contains a transaction with a timestamp after the block timestamp.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            Block block = context.ValidationContext.BlockToValidate;

            if (BlockStake.IsProofOfStake(block))
            {
                // Coinbase output should be empty if proof-of-stake block.
                if ((block.Transactions[0].Outputs.Count != 1) || !block.Transactions[0].Outputs[0].IsEmpty)
                {
                    this.Logger.LogTrace("(-)[COINBASE_NOT_EMPTY]");
                    ConsensusErrors.BadStakeBlock.Throw();
                }

                // Second transaction must be coinstake, the rest must not be.
                if (!block.Transactions[1].IsCoinStake)
                {
                    this.Logger.LogTrace("(-)[NO_COINSTAKE]");
                    ConsensusErrors.BadStakeBlock.Throw();
                }

                if (block.Transactions.Skip(2).Any(t => t.IsCoinStake))
                {
                    this.Logger.LogTrace("(-)[MULTIPLE_COINSTAKE]");
                    ConsensusErrors.BadMultipleCoinstake.Throw();
                }
            }

            // Check transactions.
            foreach (Transaction transaction in block.Transactions)
            {
                // Check transaction timestamp.
                if (block.Header.Time < transaction.Time)
                {
                    this.Logger.LogTrace("Block contains transaction with timestamp {0}, which is greater than block's timestamp {1}.", transaction.Time, block.Header.Time);
                    this.Logger.LogTrace("(-)[TX_TIME_MISMATCH]");
                    ConsensusErrors.BlockTimeBeforeTrx.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}