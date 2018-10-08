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


            return null; // TODO POA
        }

        public bool IsValidTimestamp(uint headerUnixTimestamp)
        {
            return false; // TODO POA
        }
    }
}
