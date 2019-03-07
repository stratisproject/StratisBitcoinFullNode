using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    /// <summary>
    /// Basic implementation of <see cref="IEvent"/>.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.IEvent" />
    public class EventBase : IEvent
    {
        /// <inheritdoc />
        public Guid CorrelationId { get; internal set; }

        public override string ToString()
        {
            return $"{this.CorrelationId.ToString()} - {this.GetType().Name}";
        }
    }
}
