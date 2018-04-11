using System;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    /// <summary>
    /// This date time provider substitutes the node's usual DTP when running certain
    /// integration tests so that we can generate coins faster.
    /// </summary>
    public sealed class GenerateCoinsFastDateTimeProvider : IDateTimeProvider
    {
        private TimeSpan adjustedTimeOffset;
        private DateTime startFrom;

        public GenerateCoinsFastDateTimeProvider()
        {
            this.adjustedTimeOffset = TimeSpan.Zero;
            this.startFrom = new DateTime(2018, 1, 1);
        }

        public long GetTime()
        {
            return this.startFrom.ToUnixTimestamp();
        }

        public DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// This gets called when the Transaction's time gets set in <see cref="Features.Miner.PowBlockAssembler"/>.
        /// <para>
        /// Please see the <see cref="Features.Miner.PowBlockAssembler.CreateCoinbase"/> method.
        /// </para>
        /// </summary>
        public DateTime GetAdjustedTime()
        {
            this.startFrom = this.startFrom.AddSeconds(5);
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the Block Header's time gets set in <see cref="Features.Miner.PowBlockAssembler"/>.
        /// <para>
        /// Please see the <see cref="Features.Miner.PowBlockAssembler.UpdateHeaders"/> method.
        /// </para>
        /// <para>
        /// Add 5 seconds to the time so that the block header's time stamp is after
        /// the transaction's creation time.
        /// </para>
        /// </summary>
        public DateTimeOffset GetTimeOffset()
        {
            this.startFrom = this.startFrom.AddSeconds(1);
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the coin stake block gets created in <see cref="Features.Miner.PosMinting"/>.
        /// <para>
        /// Please see the <see cref="Features.Miner.PosMinting.GenerateBlocksAsync"/> method.
        /// </para>
        /// </summary>
        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return new DateTimeOffset(this.startFrom.AddMinutes(118)).ToUnixTimeSeconds();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset)
        {
            this.adjustedTimeOffset = adjustedTimeOffset;
        }
    }
}