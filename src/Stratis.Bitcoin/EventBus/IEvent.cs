using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    /// <summary>
    /// Base event interface.
    /// We can requires to specify a Sender if we want (who produced the event), preferably as WeakReference
    /// </summary>
    interface IEvent
    {
        /// <summary>
        /// Gets or sets the progressive id.
        /// </summary>
        /// <value>
        /// The progressive id.
        /// </value>
        ulong Progressive { get; }
    }
}
