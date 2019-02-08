using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Signals
{
    public interface ISignals
    {
        EventNotifier<ChainedHeaderBlock> OnBlockConnected { get; }

        EventNotifier<ChainedHeaderBlock> OnBlockDisconnected { get; }

        EventNotifier<Transaction> OnTransactionReceived { get; }
    }

    public class Signals : ISignals
    {
        public Signals()
        {
            this.OnBlockConnected = new EventNotifier<ChainedHeaderBlock>();
            this.OnBlockDisconnected = new EventNotifier<ChainedHeaderBlock>();
            this.OnTransactionReceived = new EventNotifier<Transaction>();
        }

        public EventNotifier<ChainedHeaderBlock> OnBlockConnected { get; private set; }

        public EventNotifier<ChainedHeaderBlock> OnBlockDisconnected { get; private set; }

        public EventNotifier<Transaction> OnTransactionReceived { get; private set; }
    }

    public class EventNotifier<T>
    {
        private readonly List<Action<T>> callbacks;

        public EventNotifier()
        {
            this.callbacks = new List<Action<T>>();
        }

        public void Attach(Action<T> callback)
        {
            this.callbacks.Add(callback);
        }

        public void Detach(Action<T> callback)
        {
            this.callbacks.Remove(callback);
        }

        public void Notify(T item)
        {
            foreach (Action<T> callback in this.callbacks)
                callback(item);
        }
    }
}
