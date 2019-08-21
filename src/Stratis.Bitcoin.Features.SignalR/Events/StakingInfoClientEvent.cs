using System;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.Features.Miner.Models;

namespace Stratis.Bitcoin.Features.SignalR.Events
{
    /// <summary>
    /// Marker type for Client
    /// </summary>
    public class StakingInfo
    {
    }

    public class StakingInfoClientEvent : GetStakingInfoModel, IClientEvent
    {
        public StakingInfoClientEvent(GetStakingInfoModel stakingInfoModel)
        {
            if (null != stakingInfoModel)
            {
                this.Enabled = stakingInfoModel.Enabled;
                this.Staking = stakingInfoModel.Staking;
                this.Difficulty = stakingInfoModel.Difficulty;
                this.Immature = stakingInfoModel.Immature;
                this.Weight = stakingInfoModel.Weight;
                this.NetStakeWeight = stakingInfoModel.NetStakeWeight;
                this.ExpectedTime = stakingInfoModel.ExpectedTime;
                this.PooledTx = stakingInfoModel.PooledTx;
                this.SearchInterval = stakingInfoModel.SearchInterval;
                this.CurrentBlockSize = stakingInfoModel.CurrentBlockSize;
                this.CurrentBlockTx = stakingInfoModel.CurrentBlockTx;
            }
        }

        public Type NodeEventType => typeof(StakingInfo);

        public void BuildFrom(EventBase @event)
        {
            throw new NotImplementedException();
        }
    }
}