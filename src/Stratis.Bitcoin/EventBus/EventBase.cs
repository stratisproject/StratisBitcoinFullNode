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
        public ulong Progressive { get; internal set; }
    }
}
