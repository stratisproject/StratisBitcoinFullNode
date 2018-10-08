using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    public class SlotsManager
    {
        private readonly PoANetwork network;

        private readonly ILogger logger;

        public SlotsManager(Network network, ILoggerFactory loggerFactory)
        {
            this.network = Guard.NotNull(network as PoANetwork, nameof(network));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public PubKey GetPubKeyForTimestamp(uint headerUnixTimestamp)
        {
            if (!this.IsValidTimestamp(headerUnixTimestamp))
                return null;

            List<PubKey> keys = this.network.FederationPublicKeys;

            // Find timestamp of round's start.
            uint roundTime = (uint)keys.Count * this.network.TargetSpacingSeconds;
            uint roundStartTimestamp = (headerUnixTimestamp / roundTime) * roundTime;

            int currentSlotNumber = (int)((headerUnixTimestamp - roundStartTimestamp) / this.network.TargetSpacingSeconds);

            return keys[currentSlotNumber];
        }

        //TODO POA logs

        //TODO POA get my timestamp- getMyNextTimeStamp(uint currentAdjustedTime)


        public bool IsValidTimestamp(uint headerUnixTimestamp)
        {
            return headerUnixTimestamp % this.network.TargetSpacingSeconds == 0;
        }
    }
}
