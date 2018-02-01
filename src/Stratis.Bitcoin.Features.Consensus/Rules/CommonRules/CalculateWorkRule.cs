using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoW header.
    /// </summary>
    /// <exception cref="ConsensusErrors.HighHash">Thrown if block doesn't have a valid PoW header.</exception>
    public class CalculateWorkRule : ConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            if (context.CheckPow && !context.BlockValidationContext.Block.Header.CheckProofOfWork(context.Consensus))
                ConsensusErrors.HighHash.Throw();

            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetWorkRequired(context.Consensus);

            return Task.CompletedTask;
        }
    }
}