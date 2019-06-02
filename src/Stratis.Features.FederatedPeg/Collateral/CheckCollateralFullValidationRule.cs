using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.FederatedPeg.Collateral
{
    /// <summary>Ensures that collateral requirement on counterpart chain is fulfilled for the federation member that produced a block.</summary>
    /// <remarks>Ignored in IBD.</remarks>
    public class CheckCollateralFullValidationRule : FullValidationConsensusRule
    {
        private readonly IInitialBlockDownloadState ibdState;

        private readonly ICollateralChecker collateralChecker;

        private readonly ISlotsManager slotsManager;

        private readonly IDateTimeProvider dateTime;

        /// <summary>For how many seconds the block should be banned in case collateral check failed.</summary>
        private readonly int collateralCheckBanDurationSeconds;

        public CheckCollateralFullValidationRule(IInitialBlockDownloadState ibdState, ICollateralChecker collateralChecker,
            ISlotsManager slotsManager, IDateTimeProvider dateTime, Network network)
        {
            this.collateralCheckBanDurationSeconds = (int)(network.Consensus.Options as PoAConsensusOptions).TargetSpacingSeconds / 2;

            this.ibdState = ibdState;
            this.collateralChecker = collateralChecker;
            this.slotsManager = slotsManager;
            this.dateTime = dateTime;
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
                context.ValidationContext.RejectUntil = this.dateTime.GetUtcNow() + TimeSpan.FromSeconds(this.collateralCheckBanDurationSeconds);

                this.Logger.LogTrace("(-)[BAD_COLLATERAL]");
                PoAConsensusErrors.InvalidCollateralAmount.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
