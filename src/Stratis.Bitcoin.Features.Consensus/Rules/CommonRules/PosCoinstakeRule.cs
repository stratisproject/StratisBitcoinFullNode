using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Context checks on a POS block.
    /// </summary>
    public class PosCoinstakeRule : PosConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

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