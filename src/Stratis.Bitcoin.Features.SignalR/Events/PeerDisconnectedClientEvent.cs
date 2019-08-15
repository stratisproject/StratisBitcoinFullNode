

namespace Stratis.Bitcoin.Features.SignalR.Events
{
    using System;
    using Stratis.Bitcoin.EventBus;
    using Stratis.Bitcoin.EventBus.CoreEvents;

    public class PeerDisconnectedClientEvent : IClientEvent
    {
        public bool Inbound { get; set; }

        public string CorrelationId { get; set; }

        public string PeerEndPoint { get; set; }
        
        public string Reason { get; set; }

        public Type NodeEventType { get; } = typeof(PeerDisconnected);

        public void BuildFrom(EventBase @event)
        {
            if (@event is PeerDisconnected peerDisconnected)
            {
                this.Inbound = peerDisconnected.Inbound;
                this.CorrelationId = peerDisconnected.CorrelationId.ToString();
                this.PeerEndPoint = peerDisconnected.PeerEndPoint.ToString();
                this.Reason = peerDisconnected.Reason;
                return;
            }

            throw new ArgumentException();
        }
    }
}