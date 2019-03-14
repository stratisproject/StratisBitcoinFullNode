using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.EventBus
{
    /// <summary>
    /// Basic implementation of a generic <see cref="EventBase"/> that exposes a typed Content property.
    /// This is abstract to force to create a specific event.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    /// <typeparam name="TContent">The type of the content.</typeparam>
    /// <seealso cref="Stratis.Bitcoin.EventBus.EventBase" />
    public abstract class GenericEventBase<TContent> : EventBase
    {
        /// <summary>
        /// Gets or sets the content of the event.
        /// </summary>
        /// <value>
        /// The event content.
        /// </value>
        public TContent Content { get; protected set; }

        /// <summary>
        /// Create a new instance of the GenericEventBase class.
        /// </summary>
        /// <param name="content">Content of the event</param>
        public GenericEventBase(TContent content)
        {
            this.Content = content;
        }
    }
}
