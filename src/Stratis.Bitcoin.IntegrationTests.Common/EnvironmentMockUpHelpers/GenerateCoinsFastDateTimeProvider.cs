using System;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    /// <summary>
    /// This date time provider substitutes the node's usual DTP when running certain
    /// integration tests so that we can generate coins faster.
    /// </summary>
    public sealed class GenerateCoinsFastDateTimeProvider : IDateTimeProvider
    {
        private static TimeSpan adjustedTimeOffset;
        private static DateTime startFrom;

        static GenerateCoinsFastDateTimeProvider()
        {
            adjustedTimeOffset = TimeSpan.Zero;
            startFrom = new DateTime(2018, 1, 1);
        }

        public GenerateCoinsFastDateTimeProvider(ISignals signals)
        {
            signals.OnBlockConnected.Attach(this.OnBlockConnected);
        }

        public long GetTime()
        {
            return startFrom.ToUnixTimestamp();
        }

        public DateTime GetUtcNow()
        {
            return startFrom;
        }

        /// <summary>
        /// This gets called when the Transaction's time gets set in <see cref="Features.Miner.PowBlockDefinition"/>.
        /// </summary>
        public DateTime GetAdjustedTime()
        {
            return startFrom;
        }

        /// <summary>
        /// This gets called when the Block Header's time gets set in <see cref="Features.Miner.PowBlockDefinition"/>.
        /// <para>
        /// Please see the <see cref="Features.Miner.PowBlockDefinition.UpdateHeaders"/> method.
        /// </para>
        /// <para>
        /// Add 5 seconds to the time so that the block header's time stamp is after
        /// the transaction's creation time.
        /// </para>
        /// </summary>
        public DateTimeOffset GetTimeOffset()
        {
            return startFrom;
        }

        /// <summary>
        /// This gets called when the coin stake block gets created in <see cref="PosMinting"/>.
        /// This gets called when the transaction's time gets set in <see cref="Features.Miner.PowBlockDefinition"/>.
        /// <para>
        /// Please see the <see cref="PosMinting.GenerateBlocksAsync"/> method.
        /// </para>
        /// <para>
        /// Please see the <see cref="Features.Miner.PowBlockDefinition.CreateCoinbase"/> method.
        /// </para>
        /// </summary>
        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return startFrom.ToUnixTimestamp();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjusted)
        {
            adjustedTimeOffset = adjusted;
        }

        /// <summary>
        /// Every time a new block gets generated, this date time provider will be signaled,
        /// updating the last block time by 65 seconds.
        /// </summary>
        private void OnBlockConnected(ChainedHeaderBlock value)
        {
            startFrom = startFrom.AddSeconds(65);
        }
    }
}