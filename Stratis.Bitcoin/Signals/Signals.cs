using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Signals
{
    /// <summary>
    /// Provider of notifications of new blocks and transactions.
    /// </summary>
    public interface ISignals
    {
        /// <summary>
        /// Notify subscribers about a new transaction being available.
        /// </summary>
        /// <param name="trx">Newly added transaction.</param>
        void SignalTransaction(Transaction trx);

        /// <summary>
        /// Notify subscribers about a new block being available.
        /// </summary>
        /// <param name="block">Newly added block.</param>
        void SignalBlock(Block block);
    }

    /// <inheritdoc />
    public class Signals : ISignals
    {
        /// <summary>
        /// Initializes the object with newly created instances of signalers.
        /// </summary>
        public Signals() : this(new Signaler<Block>(), new Signaler<Transaction>())
        {
        }

        /// <summary>
        /// Initializes the object with specific signalers.
        /// </summary>
        /// <param name="blockSignaler">Signaler providing notifications about newly available blocks to its subscribers.</param>
        /// <param name="transactionSignaler">Signaler providing notifications about newly available transactions to its subscribers.</param>
        public Signals(ISignaler<Block> blockSignaler, ISignaler<Transaction> transactionSignaler)
        {
            Guard.NotNull(blockSignaler, nameof(blockSignaler));
            Guard.NotNull(transactionSignaler, nameof(transactionSignaler));

            this.Blocks = blockSignaler;
            this.Transactions = transactionSignaler;
        }

        /// <summary>Signaler providing notifications about newly available blocks to its subscribers.</summary>
        public ISignaler<Block> Blocks { get; }

        /// <summary>Signaler providing notifications about newly available transactions to its subscribers.</summary>
        public ISignaler<Transaction> Transactions { get; }

        /// <inheritdoc />
        /// <remarks>TODO: Remove guard - Broadcast has its own guard.</remarks>
        public void SignalBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));

            this.Blocks.Broadcast(block);
        }

        /// <inheritdoc />
        /// <remarks>TODO: Remove guard - Broadcast has its own guard.</remarks>
        public void SignalTransaction(Transaction trx)
        {
            Guard.NotNull(trx, nameof(trx));

            this.Transactions.Broadcast(trx);
        }
    }
}