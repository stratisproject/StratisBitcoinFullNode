using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Calculate the difficulty for a POW network and check that it is correct.   
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class CheckDifficultyPowRule : ConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.HighHash"> Thrown if block doesn't have a valid PoS header.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (!context.MinedBlock && !context.ValidationContext.Block.Header.CheckProofOfWork())
                ConsensusErrors.HighHash.Throw();

            Target nextWorkRequired = context.ValidationContext.ChainedHeader.GetWorkRequired(context.Consensus);

            BlockHeader header = context.ValidationContext.Block.Header;

            // Check proof of work.
            if (header.Bits != nextWorkRequired)
            {
                this.Logger.LogTrace("(-)[BAD_DIFF_BITS]");
                ConsensusErrors.BadDiffBits.Throw();
            }

            return Task.CompletedTask;
        }
    }
}