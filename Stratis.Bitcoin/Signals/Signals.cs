using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;

namespace Stratis.Bitcoin.Signals
{
    /// <summary>
    /// Provider of notifications of new blocks and transactions.
    /// </summary>
    public interface ISignals
    {
        /// <summary>
        /// Notify subscribers about a new block being available.
        /// </summary>
        /// <param name="block">Newly added block.</param>
        void SignalBlock(Block block);

        /// <summary>
        /// Notify subscribers about a new transaction being available.
        /// </summary>
        /// <param name="trx">Newly added transaction.</param>
        void SignalTransaction(Transaction trx);

        /// <summary>
        /// Subscribes to receive notifications when a new block is available.
        /// </summary>
        /// <param name="observer">Observer to be subscribed to receive signaler's messages.</param>
        /// <returns>Disposable object to allow observer to unsubscribe from the signaler.</returns>
        IDisposable SubscribeForBlocks(IObserver<Block> observer);

        /// <summary>
        /// Subscribes to receive notifications when a new transaction is available.
        /// </summary>
        /// <param name="observer">Observer to be subscribed to receive signaler's messages.</param>
        /// <returns>Disposable object to allow observer to unsubscribe from the signaler.</returns>
        IDisposable SubscribeForTransactions(IObserver<Transaction> observer);
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

            this.blocks = blockSignaler;
            this.transactions = transactionSignaler;
        }

        /// <summary>Signaler providing notifications about newly available blocks to its subscribers.</summary>
        private ISignaler<Block> blocks { get; }

        /// <summary>Signaler providing notifications about newly available transactions to its subscribers.</summary>
        private ISignaler<Transaction> transactions { get; }

        /// <inheritdoc />
        public void SignalBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));

            this.blocks.Broadcast(block);
        }

        /// <inheritdoc />
        public void SignalTransaction(Transaction trx)
        {
            Guard.NotNull(trx, nameof(trx));

            this.transactions.Broadcast(trx);
        }

        /// <inheritdoc />
        public IDisposable SubscribeForBlocks(IObserver<Block> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.blocks.Subscribe(observer);
        }

        /// <inheritdoc />
        public IDisposable SubscribeForTransactions(IObserver<Transaction> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.transactions.Subscribe(observer);
        }
    }
}