using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class StakingBroadcaster : ClientBroadcasterBase
    {
        private readonly IPosMinting posMinting;

        public StakingBroadcaster(
            ILoggerFactory loggerFactory,
            IPosMinting posMinting,
            INodeLifetime nodeLifetime,
            IAsyncProvider asyncProvider,
            EventsHub eventsHub)
            : base(eventsHub, loggerFactory, nodeLifetime, asyncProvider)
        {
            this.posMinting = posMinting;
        }

        protected override IEnumerable<IClientEvent> GetMessages()
        {
            if (null != this.posMinting)
            {
                yield return new StakingInfoClientEvent(this.posMinting.GetGetStakingInfoModel());
            }
        }
    }
}