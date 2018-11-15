using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.SmartContracts.IntegrationTests.PoA.MockChain
{
    public class TargetSpacingDateTimeProvider : IDateTimeProvider
    {
        private DateTime backing;
        private readonly uint spacing;

        public TargetSpacingDateTimeProvider(Network network)
        {
            this.backing = DateTime.Now;
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
