namespace Stratis.Bitcoin.Features.SignalR.Events
{
    using System;
    using Stratis.Bitcoin.EventBus;
    using Stratis.Bitcoin.EventBus.CoreEvents;

    public class PeerConnectedClientEvent : IClientEvent
    {
        public bool Inbound { get; set; }

        public string CorrelationId { get; set; }

        public string PeerEndPoint { get; set; }

        public Type NodeEventType { get; } = typeof(PeerConnected);

        public void BuildFrom(EventBase @event)
        {
            if (@event is PeerConnected peerConnected)
            {
                this.Inbound = peerConnected.Inbound;
                this.CorrelationId = peerConnected.CorrelationId.ToString();
                this.PeerEndPoint = peerConnected.PeerEndPoint.ToString();
                return;
            }

            throw new ArgumentException();
        }
    }
}