using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// This rule will validate the block size and weight.
    /// </summary>
    public class BlockSizeRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockWeight">The block weight is higher than the max block weight.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">The block length is larger than the allowed max block base size.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">The amount of transactions inside the block is higher than the allowed max block base size.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">The block does not contain any transactions.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            var consensus = this.Parent.Network.Consensus;

            // After the coinbase witness nonce and commitment are verified,
            // we can check if the block weight passes (before we've checked the
            // coinbase witness, it would be possible for the weight to be too
            // large by filling up the coinbase witness, which doesn't change
            // the block hash, so we couldn't mark the block as permanently
            // failed).
            if (context.ValidationContext.BlockToValidate.GetBlockWeight(consensus) > consensus.Options.MaxBlockWeight)
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_WEIGHT]");
                ConsensusErrors.BadBlockWeight.Throw();
            }

            Block block = context.ValidationContext.BlockToValidate;

            // Size limits.
            if ((block.Transactions.Count == 0) || (block.Transactions.Count > consensus.Options.MaxBlockBaseSize) ||
                (block.GetSize(TransactionOptions.None, this.Parent.Network.Consensus.ConsensusFactory) > consensus.Options.MaxBlockBaseSize))
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_LEN]");
                ConsensusErrors.BadBlockLength.Throw();
            }

            return Task.CompletedTask;
        }
    }
}