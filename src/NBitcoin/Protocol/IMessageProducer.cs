using System;
using System.Collections.Generic;

namespace NBitcoin.Protocol
{
    public class MessageProducer<T>
    {
        private List<IMessageListener<T>> listeners = new List<IMessageListener<T>>();

        public IDisposable AddMessageListener(IMessageListener<T> listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            lock (this.listeners)
            {
                return new Scope(() =>
                {
                    this.listeners.Add(listener);
                }, () =>
                {
                    lock (this.listeners)
                    {
                        this.listeners.Remove(listener);
                    }
                });
            }
        }

        public void RemoveMessageListener(IMessageListener<T> listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            lock (this.listeners)
            {
                this.listeners.Add(listener);
            }
        }

        public void PushMessage(T message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            lock (this.listeners)
            {
                foreach (IMessageListener<T> listener in this.listeners)
                {
                    listener.PushMessage(message);
                }
            }
        }


        public void PushMessages(IEnumerable<T> messages)
        {
            if (messages == null)
                throw new ArgumentNullException("messages");

            lock (this.listeners)
            {
                foreach (T message in messages)
                {
                    if (message == null)
                        throw new ArgumentNullException("message");

                    foreach (IMessageListener<T> listener in this.listeners)
                    {
                        listener.PushMessage(message);
                    }
                }
            }
        }
    }
}