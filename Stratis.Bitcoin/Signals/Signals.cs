using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Signals
{
    /// <summary>
    /// Provider of notifications of new blocks and transactions.
    /// </summary>
    public interface ISignals
    {
        /// <summary>Signaler providing notifications about newly available blocks to its subscribers.</summary>
        /// <remarks>TODO: Consider making this private (i.e. remove it from the interface) as it seems that this is being misused with code like:
        /// <code>
        /// this.signals.Blocks.Broadcast(block.Block);
        /// </code>
        /// <para>
        /// which should probably be instead:
        /// </para>
        /// <code>
        /// this.signals.Signal(block.Block);
        /// </code>
        /// </remarks>
        ISignaler<Block> Blocks { get; }

        /// <summary>Signaler providing notifications about newly available transactions to its subscribers.</summary>
        /// <remarks>TODO: Consider making this private (i.e. remove it from the interface) - see <see cref="Blocks"/>'s remarks.
        ISignaler<Transaction> Transactions { get; }

        /// <summary>
        /// Notify subscribers about a new transaction being available.
        /// </summary>
        /// <param name="trx">Newly added transaction.</param>
        void Signal(Transaction trx);

        /// <summary>
        /// Notify subscribers about a new block being available.
        /// </summary>
        /// <param name="block">Newly added block.</param>
        void Signal(Block block);
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

        /// <inheritdoc />
        public ISignaler<Block> Blocks { get; }

        /// <inheritdoc />
        public ISignaler<Transaction> Transactions { get; }

        /// <inheritdoc />
        /// <remarks>TODO: Remove guard - Broadcast has its own guard.</remarks>
        public void Signal(Block block)
        {
            Guard.NotNull(block, nameof(block));

            this.Blocks.Broadcast(block);
        }

        /// <inheritdoc />
        /// <remarks>TODO: Remove guard - Broadcast has its own guard.</remarks>
        public void Signal(Transaction trx)
        {
            Guard.NotNull(trx, nameof(trx));

            this.Transactions.Broadcast(trx);
        }
    }
}