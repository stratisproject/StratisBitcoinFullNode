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
            
            // An unexpected consequence of serialization is to change the Block.BlockSize property
            // This rule serializes the block in a few ways to determine the block weight and as
            // a result changes the BlockSize property unintentionally, to fix this we create a new instance of the Block
            Block block = consensus.ConsensusFactory.CreateBlock();
            block.ReadWrite(context.ValidationContext.BlockToValidate.ToBytes(consensus.ConsensusFactory), consensus.ConsensusFactory);

            // After the coinbase witness nonce and commitment are verified,
            // we can check if the block weight passes (before we've checked the
            // coinbase witness, it would be possible for the weight to be too
            // large by filling up the coinbase witness, which doesn't change
            // the block hash, so we couldn't mark the block as permanently
            // failed).
            if (block.GetBlockWeight(consensus) > consensus.Options.MaxBlockWeight)
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_WEIGHT]");
                ConsensusErrors.BadBlockWeight.Throw();
            }

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