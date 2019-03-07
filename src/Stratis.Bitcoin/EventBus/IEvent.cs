using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    /// <summary>
    /// Base event interface.
    /// We can requires to specify a Sender if we want (who produced the event), preferably as WeakReference
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Gets the correlation identifier.
        /// </summary>
        /// <value>
        /// The correlation identifier.
        /// </value>
        Guid CorrelationId { get; }
    }
}
