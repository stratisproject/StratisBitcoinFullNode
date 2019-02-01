using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Primitives;

namespace Stratis.Bitcoin.Signals
{
    public interface ISignals
    {
        event Signals.BlockDelegate OnBlockConnected;

        event Signals.BlockDelegate OnBlockDisconnected;

        event Signals.TransactionDelegate OnTransactionAvailable;

        void TriggerBlockDisconnected(ChainedHeaderBlock chainedHeaderBlock);

        void TriggerBlockConnected(ChainedHeaderBlock chainedHeaderBlock);

        void TriggerTransactionAvailable(Transaction transaction);
    }

    public class Signals : ISignals
    {
        public delegate void BlockDelegate(ChainedHeaderBlock chainedHeaderBlock);

        public delegate void TransactionDelegate(Transaction transaction);

        public event BlockDelegate OnBlockConnected;

        public event BlockDelegate OnBlockDisconnected;

        /// <summary>Even that is executed when tranasction is received from another peer.</summary>
        public event TransactionDelegate OnTransactionAvailable;

        public void TriggerBlockDisconnected(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.OnBlockDisconnected?.Invoke(chainedHeaderBlock);
        }

        public void TriggerBlockConnected(ChainedHeaderBlock chainedHeaderBlock)
        {
            this.OnBlockConnected?.Invoke(chainedHeaderBlock);
        }

        public void TriggerTransactionAvailable(Transaction transaction)
        {
            this.OnTransactionAvailable?.Invoke(transaction);
        }
    }
}
