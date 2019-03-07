using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    public class InMemoryEventBus : IEventBus
    {
        /// <summary>
        /// The subscriptions stored by EventType
        /// </summary>
        private readonly Dictionary<Type, List<ISubscription>> subscriptions;

        /// <summary>
        /// The subscriptions lock to prevent race condition during publishing
        /// </summary>
        private static readonly object SubscriptionsLock = new object();

        public InMemoryEventBus()
        {
            this.subscriptions = new Dictionary<Type, List<ISubscription>>();
        }

        /// <inheritdoc />
        public SubscriptionToken Subscribe<TEventBase>(Action<TEventBase> action) where TEventBase : EventBase
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (SubscriptionsLock)
            {
                if (!this.subscriptions.ContainsKey(typeof(TEventBase)))
                    this.subscriptions.Add(typeof(TEventBase), new List<ISubscription>());

                var token = new SubscriptionToken(this, typeof(TEventBase));
                this.subscriptions[typeof(TEventBase)].Add(new Subscription<TEventBase>(action, token));
                return token;
            }
        }

        /// <inheritdoc />
        public void Unsubscribe(SubscriptionToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            lock (SubscriptionsLock)
            {
                if (this.subscriptions.ContainsKey(token.EventType))
                {
                    var allSubscriptions = this.subscriptions[token.EventType];

                    var subscriptionToRemove = allSubscriptions.FirstOrDefault(sub => sub.SubscriptionToken.Token == token.Token);
                    if (subscriptionToRemove != null)
                        this.subscriptions[token.EventType].Remove(subscriptionToRemove);
                }
            }
        }

        /// <inheritdoc />
        public void Publish<TEventBase>(TEventBase eventItem) where TEventBase : EventBase
        {
            if (eventItem == null)
                throw new ArgumentNullException(nameof(eventItem));

            // Assigns an unique id to the event.
            eventItem.CorrelationId = Guid.NewGuid();

            List<ISubscription> allSubscriptions = new List<ISubscription>();
            lock (SubscriptionsLock)
            {
                if (this.subscriptions.ContainsKey(typeof(TEventBase)))
                    allSubscriptions = this.subscriptions[typeof(TEventBase)];
            }

            for (var index = 0; index < allSubscriptions.Count; index++)
            {
                var subscription = allSubscriptions[index];
                try
                {
                    subscription.Publish(eventItem);
                }
                catch (Exception ex)
                {
                    // TODO: log failed handlers and/or call an internal error handler
                    // like this.SubscriptionErrorHandler.Handle(message, ex);
                }
            }
        }
    }
}