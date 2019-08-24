using System;
using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Features.SignalR
{
    public interface IClientEvent
    {
        Type NodeEventType { get; }

        void BuildFrom(EventBase @event);
    }
}