using System;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;

namespace Stratis.Bitcoin.Features.SignalR.Events
{
    public class BlockConnectedClientEvent : IClientEvent
    {
        public string Hash { get; set; }

        public int Height { get; set; }

        public Type NodeEventType { get; } = typeof(BlockConnected);

        public void BuildFrom(EventBase @event)
        {
            if (@event is BlockConnected blockConnected)
            {
                this.Hash = blockConnected.ConnectedBlock.ChainedHeader.HashBlock.ToString();
                this.Height = blockConnected.ConnectedBlock.ChainedHeader.Height;
                return;
            }

            throw new ArgumentException();
        }
    }
}