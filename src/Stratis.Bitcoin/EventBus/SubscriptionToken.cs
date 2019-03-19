using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    /// <summary>
    /// Represents a subscription token.
    /// </summary>
    public class SubscriptionToken : IDisposable
    {
        public IEventBus Bus { get; }

        public Guid Token { get; }

        public Type EventType { get; }

        internal SubscriptionToken(IEventBus bus, Type eventType)
        {
            this.Bus = bus;
            this.Token = Guid.NewGuid();
            this.EventType = eventType;
        }

        public void Dispose()
        {
            this.Bus.Unsubscribe(this);
        }
    }
}
