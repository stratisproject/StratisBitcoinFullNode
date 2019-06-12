using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Checks that coisntake tx from the proven header is the 2nd transaction in a block and that block has at least 2 transactions.</summary>
    public class PosTransactionsOrderRule : IntegrityValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidTxCount">Thrown in case block has less than 2 transaction.</exception>
        /// <exception cref="ConsensusErrors.PHCoinstakeMissmatch">Thrown in case coinstake transaction from the proven header missmatches 2nd block transaction.</exception>
        public override void Run(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            if (block.Transactions.Count < 2)
            {
                this.Logger.LogTrace("(-)[BAD_TX_COUNT]");
                ConsensusErrors.InvalidTxCount.Throw();
            }

            var header = context.ValidationContext.ChainedHeaderToValidate.Header as ProvenBlockHeader;

            uint256 headerCoinstakeHash = header.Coinstake.GetHash();
            uint256 blockCoinstakeHash = block.Transactions[1].GetHash();

            if (headerCoinstakeHash != blockCoinstakeHash)
            {
                this.Logger.LogTrace("(-)[COINSTAKE_TX_MISSMATCH]");
                ConsensusErrors.PHCoinstakeMissmatch.Throw();
            }
        }
    }
}
