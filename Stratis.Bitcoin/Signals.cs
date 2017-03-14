using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{
    public interface ISignals
    {
        ISignaler<Block> Blocks { get; }
        ISignaler<Transaction> Transactions { get; }

        void Signal(Transaction trx);
        void Signal(Block block);
    }

    public class Signals : ISignals
    {
        public Signals() : this(new Signaler<Block>(), new Signaler<Transaction>())
        {            
        }

        public Signals(ISignaler<Block> blockSignaler, ISignaler<Transaction> transactionSignaler)
        {
            Guard.NotNull(blockSignaler, nameof(blockSignaler));
            Guard.NotNull(transactionSignaler, nameof(transactionSignaler));

            this.Blocks = blockSignaler;
            this.Transactions = transactionSignaler;
        }

        public ISignaler<Block> Blocks { get; }
        public ISignaler<Transaction> Transactions { get; }

        public void Signal(Block block)
        {
            Guard.NotNull(block, nameof(block));

            this.Blocks.Broadcast(block);
        }

        public void Signal(Transaction trx)
        {
            Guard.NotNull(trx, nameof(trx));

            this.Transactions.Broadcast(trx);
        }
    }
}