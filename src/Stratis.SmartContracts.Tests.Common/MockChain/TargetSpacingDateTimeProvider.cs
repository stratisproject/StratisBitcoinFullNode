using System;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    public class TargetSpacingDateTimeProvider : IDateTimeProvider
    {
        private DateTime backing;
        private readonly uint spacing;

        public TargetSpacingDateTimeProvider(Network network)
        {
            this.backing = DateTimeOffset.FromUnixTimeSeconds(network.GenesisTime + 1).UtcDateTime;
            this.spacing = (network.Consensus.Options as PoAConsensusOptions).TargetSpacingSeconds;
        }

        public long GetTime()
        {
            return this.backing.ToUnixTimestamp();
        }

        public DateTime GetUtcNow()
        {
            return this.backing;
        }

        public DateTime GetAdjustedTime()
        {
            return this.backing;
        }

        public DateTimeOffset GetTimeOffset()
        {
            return this.backing;
        }

        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return this.backing.ToUnixTimestamp();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjusted)
        {
            throw new NotImplementedException();
        }

        public void NextSpacing()
        {
            this.backing = this.backing.AddSeconds(this.spacing);
        }
    }
}
