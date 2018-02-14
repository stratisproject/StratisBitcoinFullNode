using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Context checks on a POS block.
    /// </summary>
    public class PosFutureDriftRule : PosConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

            // Check timestamp.
            if (block.Header.Time > this.FutureDrift(this.Parent.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp()))
            {
                // The block can be valid only after its time minus the future drift.
                context.BlockValidationContext.RejectUntil = Utils.UnixTimeToDateTime(block.Header.Time - this.FutureDrift(0)).UtcDateTime;
                this.Logger.LogTrace("(-)[TIME_TOO_FAR]");
                ConsensusErrors.BlockTimestampTooFar.Throw();
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies future drift to provided timestamp.
        /// </summary>
        /// <remarks>
        /// Future drift is maximal allowed block's timestamp difference over adjusted time.
        /// If this difference is greater block won't be accepted.
        /// </remarks>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns>Timestamp with maximum future drift applied.</returns>
        private long FutureDrift(long time)
        {
            return this.IsDriftReduced(time) ? time + 15 : time + 128 * 60 * 60;
        }

        /// <summary>
        /// Checks whether the future drift should be reduced after provided timestamp.
        /// </summary>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns><c>true</c> if for this timestamp future drift should be reduced, <c>false</c> otherwise.</returns>
        private bool IsDriftReduced(long time)
        {
            return time > PosConsensusValidator.DriftingBugFixTimestamp;
        }
    }
}