using System.Threading.Tasks;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>Ensures that collateral requirement on counterpart chain is fulfilled for the federation member that produced a block.</summary>
    /// <remarks>Ignored in IBD.</remarks>
    public class CheckCollateralFullValidationRule : FullValidationConsensusRule
    {
        private readonly IInitialBlockDownloadState ibdState;

        private readonly CollateralFederationManager federationManager;

        public CheckCollateralFullValidationRule(IInitialBlockDownloadState ibdState, CollateralFederationManager federationManager)
        {
            this.ibdState = ibdState;
            this.federationManager = federationManager;
        }

        public override Task RunAsync(RuleContext context)
        {
            // TODO implement the rule
            return Task.CompletedTask;
        }
    }
}
