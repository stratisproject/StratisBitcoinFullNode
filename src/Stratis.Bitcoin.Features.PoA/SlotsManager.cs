using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    // TODO POA add comment
    // TODO POA logs
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

            // Time when current round started.
            uint roundStartTimestamp = (headerUnixTimestamp / roundTime) * roundTime;

            // Slot number in current round.
            int currentSlotNumber = (int)((headerUnixTimestamp - roundStartTimestamp) / this.network.TargetSpacingSeconds);

            return keys[currentSlotNumber];
        }

        public uint GetMyMiningTimestamp(uint currentTime)
        {
            //TODO POA get my timestamp- getMyNextTimeStamp(uint currentAdjustedTime)

            return 0;
        }

        public bool IsValidTimestamp(uint headerUnixTimestamp)
        {
            return headerUnixTimestamp % this.network.TargetSpacingSeconds == 0;
        }
    }
}
