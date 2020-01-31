using System;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.ColdStaking.Models;

namespace Stratis.Bitcoin.Features.SignalR.Events
{
    /// <summary>
    /// Marker type for Client
    /// </summary>
    public class ColdStakingInfo
    {
    }

    public class ColdStakingInfoClientEvent : GetColdStakingInfoResponse, IClientEvent
    {
        public ColdStakingInfoClientEvent(GetColdStakingInfoResponse coldStakingInfo)
        {
            if (null != coldStakingInfo)
            {
                this.ColdWalletAccountExists = coldStakingInfo.ColdWalletAccountExists;
                this.HotWalletAccountExists = coldStakingInfo.HotWalletAccountExists;
            }
        }

        public Type NodeEventType => typeof(ColdStakingInfo);

        public void BuildFrom(EventBase @event)
        {
            throw new NotImplementedException();
        }
    }
}
