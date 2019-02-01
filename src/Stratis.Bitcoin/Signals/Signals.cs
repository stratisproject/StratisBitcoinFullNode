using NBitcoin;
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
        event Signals.TransactionDelegate OnTransactionReceived;

        /// <summary>Invokes <see cref="OnBlockConnected"/> event.</summary>
        void TriggerBlockConnected(ChainedHeaderBlock chainedHeaderBlock);

        /// <summary>Invokes <see cref="OnBlockDisconnected"/> event.</summary>
        void TriggerBlockDisconnected(ChainedHeaderBlock chainedHeaderBlock);

        /// <summary>Invokes <see cref="OnTransactionReceived"/> event.</summary>
        void TriggerTransactionReceived(Transaction transaction);
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
        public event TransactionDelegate OnTransactionReceived;

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
        public void TriggerTransactionReceived(Transaction transaction)
        {
            this.OnTransactionReceived?.Invoke(transaction);
        }
    }
}
