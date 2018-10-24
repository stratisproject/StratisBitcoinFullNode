using System.Collections.Generic;
using System.Diagnostics;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit
{
    /// <summary>
    /// Used to capture messages to potentially be forwarded later. Messages are forwarded by
    /// disposing of the message bus.
    /// </summary>
    public class DelayedMessageBus : IMessageBus
    {
        private readonly IMessageBus innerBus;
        private readonly List<IMessageSinkMessage> messages = new List<IMessageSinkMessage>();

        public DelayedMessageBus(IMessageBus innerBus)
        {
            this.innerBus = innerBus;
        }

        [DebuggerStepThrough]
        public bool QueueMessage(IMessageSinkMessage message)
        {
            lock (this.messages)
                this.messages.Add(message);

            // No way to ask the inner bus if they want to cancel without sending them the message, so
            // we just go ahead and continue always.
            return true;
        }

        public void Dispose()
        {
            foreach (IMessageSinkMessage message in this.messages)
                this.innerBus.QueueMessage(message);
        }
    }
}
