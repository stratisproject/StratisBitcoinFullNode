using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Signals
{
    public interface ISignals
    {
        /// <summary>Event that is executed when a block is connected to a consensus chain.</summary>
        EventNotifier<ChainedHeaderBlock> OnBlockConnected { get; }

        /// <summary>Event that is executed when a block is disconnected from a consensus chain.</summary>
        EventNotifier<ChainedHeaderBlock> OnBlockDisconnected { get; }

        /// <summary>Event that is executed when a transaction is received from another peer.</summary>
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

        /// <inheritdoc />
        public EventNotifier<ChainedHeaderBlock> OnBlockConnected { get; private set; }

        /// <inheritdoc />
        public EventNotifier<ChainedHeaderBlock> OnBlockDisconnected { get; private set; }

        /// <inheritdoc />
        public EventNotifier<Transaction> OnTransactionReceived { get; private set; }
    }

    public class EventNotifier<T>
    {
        private readonly List<Action<T>> callbacks;

        public EventNotifier()
        {
            this.callbacks = new List<Action<T>>();
        }

        /// <summary>Registers a callback which will be invoked when <see cref="Notify"/> is called.</summary>
        public void Attach(Action<T> callback)
        {
            this.callbacks.Add(callback);
        }

        /// <summary>Unregisters a callback.</summary>
        public void Detach(Action<T> callback)
        {
            this.callbacks.Remove(callback);
        }

        /// <summary>Executes all registered callbacks.</summary>
        public void Notify(T item)
        {
            foreach (Action<T> callback in this.callbacks)
                callback(item);
        }
    }
}
