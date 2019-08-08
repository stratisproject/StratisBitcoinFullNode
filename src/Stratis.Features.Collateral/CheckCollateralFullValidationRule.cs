using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Collateral
{
    /// <summary>Ensures that collateral requirement on counterpart chain is fulfilled for the federation member that produced a block.</summary>
    /// <remarks>Ignored in IBD.</remarks>
    public class CheckCollateralFullValidationRule : FullValidationConsensusRule
    {
        private readonly IInitialBlockDownloadState ibdState;

        private readonly ICollateralChecker collateralChecker;

        private readonly ISlotsManager slotsManager;

        private readonly IDateTimeProvider dateTime;

        private readonly CollateralHeightCommitmentEncoder encoder;

        private readonly Network network;

        /// <summary>For how many seconds the block should be banned in case collateral check failed.</summary>
        private readonly int collateralCheckBanDurationSeconds;

        public CheckCollateralFullValidationRule(IInitialBlockDownloadState ibdState, ICollateralChecker collateralChecker,
            ISlotsManager slotsManager, IDateTimeProvider dateTime, Network network)
        {
            this.network = network;
            this.encoder = new CollateralHeightCommitmentEncoder();
            this.ibdState = ibdState;
            this.collateralChecker = collateralChecker;
            this.slotsManager = slotsManager;
            this.dateTime = dateTime;

            this.collateralCheckBanDurationSeconds = (int)(this.network.Consensus.Options as PoAConsensusOptions).TargetSpacingSeconds / 2;
        }

        public override Task RunAsync(RuleContext context)
        {
            if (this.ibdState.IsInitialBlockDownload())
            {
                this.Logger.LogTrace("(-)[SKIPPED_IN_IBD]");
                return Task.CompletedTask;
            }

            IFederationMember federationMember = this.slotsManager.GetFederationMemberForTimestamp(context.ValidationContext.BlockToValidate.Header.Time);

            byte[] rawCommitmentData = this.encoder.ExtractRawCommitmentData(context.ValidationContext.BlockToValidate.Transactions.First());

            if (rawCommitmentData == null)
            {
                // Every PoA miner on sidechain network is enforced to include commitment data to the blocks mined.
                // Not having a commitment always should result in a permanent ban of the block.
                this.Logger.LogTrace("(-)[NO_COMMITMENT_FOUND]");
                PoAConsensusErrors.InvalidCollateralAmountNoCommitment.Throw();
            }

            int commitmentHeight = this.encoder.Decode(rawCommitmentData);
            this.Logger.LogDebug("Commitment is: {0}.", commitmentHeight);

            // TODO: Both this and CollateralPoAMiner are using this chain's MaxReorg instead of the Counter chain's MaxReorg. Beware: fixing requires fork.

            int counterChainHeight = this.collateralChecker.GetCounterChainConsensusHeight();
            int maxReorgLength = AddressIndexer.GetMaxReorgOrFallbackMaxReorg(this.network);

            // Check if commitment height is less than `mainchain consensus tip height - MaxReorg`.
            if (commitmentHeight > counterChainHeight - maxReorgLength)
            {
                // Temporary reject the block since it's possible that due to network connectivity problem counter chain is out of sync and
                // we are relying on chain state old data. It is possible that when we advance on counter chain commitment height will be
                // sufficiently old.
                context.ValidationContext.RejectUntil = this.dateTime.GetUtcNow() + TimeSpan.FromSeconds(this.collateralCheckBanDurationSeconds);

                this.Logger.LogDebug("commitmentHeight is {0}, counterChainHeight is {1}.", commitmentHeight, counterChainHeight);

                this.Logger.LogTrace("(-)[COMMITMENT_TOO_NEW]");
                PoAConsensusErrors.InvalidCollateralAmountCommitmentTooNew.Throw();
            }

            if (!this.collateralChecker.CheckCollateral(federationMember, commitmentHeight))
            {
                // By setting rejectUntil we avoid banning a peer that provided a block.
                context.ValidationContext.RejectUntil = this.dateTime.GetUtcNow() + TimeSpan.FromSeconds(this.collateralCheckBanDurationSeconds);

                this.Logger.LogTrace("(-)[BAD_COLLATERAL]");
                PoAConsensusErrors.InvalidCollateralAmount.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
