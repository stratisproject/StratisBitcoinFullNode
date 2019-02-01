﻿using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Signals
{
    public interface ISignals
    {
        /// <summary>Event that is executed when block is connected to consensus chain.</summary>
        event Signals.BlockDelegate OnBlockConnected;

        /// <summary>Event that is executed when block is disconnected from consensus chain.</summary>
        event Signals.BlockDelegate OnBlockDisconnected;

        /// <summary>Event that is executed when transaction is received from another peer.</summary>
        event Signals.TransactionDelegate OnTransactionAvailable;

        /// <summary>Invokes <see cref="OnBlockConnected"/> event.</summary>
        void TriggerBlockConnected(ChainedHeaderBlock chainedHeaderBlock);

        /// <summary>Invokes <see cref="OnBlockDisconnected"/> event.</summary>
        void TriggerBlockDisconnected(ChainedHeaderBlock chainedHeaderBlock);

        /// <summary>Invokes <see cref="OnTransactionAvailable"/> event.</summary>
        void TriggerTransactionAvailable(Transaction transaction);
    }

    public class Signals : ISignals
    {
        public delegate void BlockDelegate(ChainedHeaderBlock chainedHeaderBlock);

        public delegate void TransactionDelegate(Transaction transaction);

        /// <inheritdoc />
        public event BlockDelegate OnBlockConnected;

        /// <inheritdoc />
        public event BlockDelegate OnBlockDisconnected;

        /// <inheritdoc />
        public event TransactionDelegate OnTransactionAvailable;

        /// <inheritdoc />
        public void TriggerBlockDisconnected(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.OnBlockDisconnected?.Invoke(chainedHeaderBlock);
        }

        /// <inheritdoc />
        public void TriggerBlockConnected(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.OnBlockConnected?.Invoke(chainedHeaderBlock);
        }

        /// <inheritdoc />
        public void TriggerTransactionAvailable(Transaction transaction)
        {
            this.OnTransactionAvailable?.Invoke(transaction);
        }
    }
}
