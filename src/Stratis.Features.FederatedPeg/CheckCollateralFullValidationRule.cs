using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Features.FederatedPeg
{
    /// <summary>Ensures that collateral requirement on counterpart chain is fulfilled for the federation member that produced a block.</summary>
    /// <remarks>Ignored in IBD.</remarks>
    public class CheckCollateralFullValidationRule : FullValidationConsensusRule
    {
        private readonly IInitialBlockDownloadState ibdState;

        private readonly ICollateralChecker collateralChecker;

        private readonly ISlotsManager slotsManager;

        public CheckCollateralFullValidationRule(IInitialBlockDownloadState ibdState, ICollateralChecker collateralChecker, ISlotsManager slotsManager)
        {
            this.ibdState = ibdState;
            this.collateralChecker = collateralChecker;
            this.slotsManager = slotsManager;
        }

        public override Task RunAsync(RuleContext context)
        {
            if (this.ibdState.IsInitialBlockDownload())
            {
                this.Logger.LogTrace("(-)[SKIPPED_IN_IBD]");
                return Task.CompletedTask;
            }

            IFederationMember federationMember = this.slotsManager.GetFederationMemberForTimestamp(context.ValidationContext.BlockToValidate.Header.Time);

            if (!this.collateralChecker.CheckCollateral(federationMember))
            {
                this.Logger.LogTrace("(-)[BAD_COLLATERAL]");
                PoAConsensusErrors.InvalidCollateralAmount.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
