using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="Block"/> has a valid PoW header.
    /// </summary>
    public class CalculateWorkRule : ConsensusRule
    {
        /// <summary>
        /// This rules calculates the next difficulty target so it has to run every time.
        /// </summary>
        public override bool ValidationOnlyRule => false;

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.HighHash"> Thrown if block doesn't have a valid PoW header.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.CheckPow && !context.BlockValidationContext.Block.Header.CheckProofOfWork(context.Consensus))
                ConsensusErrors.HighHash.Throw();

            context.NextWorkRequired = context.BlockValidationContext.ChainedBlock.GetWorkRequired(context.Consensus);

            return Task.CompletedTask;
        }
    }
}