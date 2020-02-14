using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Features.Collateral
{
    /// <summary>
    /// Ensures that a block that was produced on a collateral aware network contains height commitment data in the coinbase transaction.
    /// <para>
    /// Blocks that are found to have this data missing data will have the peer that served the header, banned.
    /// </para>
    /// <para>
    /// This rule will be skipped in IBD as the blocks would have already passed consensus validation.
    /// </para>
    /// </summary>
    public sealed class CheckCollateralCommitmentHeightRule : FullValidationConsensusRule
    {
        private readonly IInitialBlockDownloadState ibdState;

        public CheckCollateralCommitmentHeightRule(IInitialBlockDownloadState ibdState)
        {
            this.ibdState = ibdState;
        }

        public override Task RunAsync(RuleContext context)
        {
            if (this.ibdState.IsInitialBlockDownload())
            {
                this.Logger.LogTrace("(-)[SKIPPED_IN_IBD]");
                return Task.CompletedTask;
            }

            var commitmentHeightEncoder = new CollateralHeightCommitmentEncoder(this.Logger);

            int? commitmentHeight = commitmentHeightEncoder.DecodeCommitmentHeight(context.ValidationContext.BlockToValidate.Transactions.First());
            if (commitmentHeight == null)
            {
                // Every PoA miner on a sidechain network is forced to include commitment data to the blocks mined.
                // Not having a commitment should always result in a permanent ban of the block.
                this.Logger.LogTrace("(-)[COLLATERAL_COMMITMENT_HEIGHT_MISSING]");
                PoAConsensusErrors.CollateralCommitmentHeightMissing.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
