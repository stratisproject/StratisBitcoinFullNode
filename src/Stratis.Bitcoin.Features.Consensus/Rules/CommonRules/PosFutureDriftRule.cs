﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// A rule that will verify the block time drift is according to the PoS consensus rules.
    /// </summary>
    public class PosFutureDriftRule : StakeStoreConsensusRule
    {
        /// <summary>Drifting Bug Fix, hardfork on Sat, 19 Nov 2016 00:00:00 GMT.</summary>
        public const long DriftingBugFixTimestamp = 1479513600;

        /// <summary>New future drift in seconds after the hardfork.</summary>
        private const int NewFutureDriftSeconds = 15;

        /// <summary>Old future drift in seconds before the hardfork.</summary>
        private const int OldFutureDriftSeconds = 128 * 60 * 60;

        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BlockTimestampTooFar">The block timestamp is too far into the future.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.Block;

            long adjustedTime = this.Parent.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp();

            // Check timestamp.
            if (block.Header.Time > adjustedTime + GetFutureDrift(adjustedTime))
            {
                // The block can be valid only after its time minus the future drift.
                context.ValidationContext.RejectUntil = Utils.UnixTimeToDateTime(block.Header.Time - GetFutureDrift(block.Header.Time)).UtcDateTime;
                this.Logger.LogTrace("(-)[TIME_TOO_FAR]");
                ConsensusErrors.BlockTimestampTooFar.Throw();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets future drift for the provided timestamp.
        /// </summary>
        /// <remarks>
        /// Future drift is maximal allowed block's timestamp difference over adjusted time.
        /// If this difference is greater block won't be accepted.
        /// </remarks>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns>Value of the future drift.</returns>
        public static long GetFutureDrift(long time)
        {
            return IsDriftReduced(time) ? NewFutureDriftSeconds : OldFutureDriftSeconds;
        }

        /// <summary>
        /// Checks whether the future drift should be reduced after provided timestamp.
        /// </summary>
        /// <param name="time">UNIX timestamp.</param>
        /// <returns><c>true</c> if for this timestamp future drift should be reduced, <c>false</c> otherwise.</returns>
        public static bool IsDriftReduced(long time)
        {
            // TODO: Break this rule to only be used by the statis chain 
            // this is a specific Stratis bug fix where the blockchain drifted 24 hour ahead as the protocol allowed that.
            // the protocol was fixed but historical blocks are still effected.
            return time > DriftingBugFixTimestamp;
        }
    }
}