using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>Checks that coinstake tx from the proven header is the 2nd transaction in a block and that the block has at least 2 transactions.</summary>
    public class PosTransactionsOrderRule : IntegrityValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.InvalidTxCount">Thrown in case block has less than 2 transaction.</exception>
        /// <exception cref="ConsensusErrors.PHCoinstakeMissmatch">Thrown in case coinstake transaction from the proven header missmatches 2nd block transaction.</exception>
        /// <exception cref="ConsensusErrors.ProofOfWorkTooHigh">Thrown in case block is a PoW block but created after the last pow height.</exception>
        public override void Run(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            var header = context.ValidationContext.ChainedHeaderToValidate.Header as ProvenBlockHeader;

            // In case we are in PoW era there might be no coinstake tx.
            // We have no way of telling if the block was supposed to be PoW or PoS so attacker
            // can trick us into thinking that all of them are PoW so no PH is required.
            if (header == null || !header.Coinstake.IsCoinStake)
            {
                // If the header represents a POW block we don't do any validation and just verify the header is not passed the last pow height and last checkpoint's height.
                if ((context.ValidationContext.ChainedHeaderToValidate.Height > this.Parent.Network.Consensus.LastPOWBlock) &&
                    (context.ValidationContext.ChainedHeaderToValidate.Height > this.Parent.Checkpoints.GetLastCheckpointHeight()))
                {
                    this.Logger.LogTrace("(-)[NOT_PH_POW_TOO_HIGH]");
                    ConsensusErrors.ProofOfWorkTooHigh.Throw();
                }

                this.Logger.LogTrace("(-)[NOT_PH]");
                return;
            }

            if (block.Transactions.Count < 2)
            {
                this.Logger.LogTrace("(-)[BAD_TX_COUNT]");
                ConsensusErrors.InvalidTxCount.Throw();
            }

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
