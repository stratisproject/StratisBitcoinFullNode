using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    public interface ISubscriptionErrorHandler
    {
        /// <summary>
        /// Handles the specified event error.
        /// </summary>
        /// <param name="event">The event that caused the error.</param>
        /// <param name="exception">The exception raised.</param>
        /// <param name="subscription">The subscription that generated the error.</param>
        void Handle(EventBase @event, Exception exception, ISubscription subscription);
    }
}