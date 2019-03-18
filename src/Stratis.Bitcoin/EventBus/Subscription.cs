using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    internal class Subscription<TEventBase> : ISubscription where TEventBase : EventBase
    {
        /// <summary>
        /// Token returned to the subscriber
        /// </summary>
        public SubscriptionToken SubscriptionToken { get; }

        /// <summary>
        /// The action to invoke when a subscripted event type is published.
        /// </summary>
        private readonly Action<TEventBase> action;

        public Subscription(Action<TEventBase> action, SubscriptionToken token)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            this.SubscriptionToken = token ?? throw new ArgumentNullException(nameof(token));
        }

        public void Publish(EventBase eventItem)
        {
            if (!(eventItem is TEventBase))
                throw new ArgumentException("Event Item is not the correct type.");

            this.action.Invoke(eventItem as TEventBase);
        }
    }
}
