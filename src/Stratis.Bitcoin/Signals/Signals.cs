using System;
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
        /// Notify subscribers about a new block being available.
        /// </summary>
        /// <param name="block">Newly added block.</param>
        void SignalBlockConnected(Block block);

        /// <summary>
        /// Notify subscribers about a block being disconnected.
        /// </summary>
        /// <param name="block">Block that was disconnected.</param>
        void SignalBlockDisconnected(Block block);

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
        IDisposable SubscribeForBlocksConnected(IObserver<Block> observer);

        /// <summary>
        /// Subscribes to receive notifications when a block was disconnected.
        /// </summary>
        /// <param name="observer">Observer to be subscribed to receive signaler's messages.</param>
        /// <returns>Disposable object to allow observer to unsubscribe from the signaler.</returns>
        IDisposable SubscribeForBlocksDisconnected(IObserver<Block> observer);

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
        public Signals() : this(new Signaler<Block>(), new Signaler<Block>(), new Signaler<Transaction>())
        {
        }

        /// <summary>
        /// Initializes the object with specific signalers.
        /// </summary>
        /// <param name="blockConnectedSignaler">Signaler providing notifications about newly available blocks to its subscribers.</param>
        /// <param name="blockDisonnectedSignaler">Signaler providing notifications about a block being disconnected to its subscribers.</param>
        /// <param name="transactionSignaler">Signaler providing notifications about newly available transactions to its subscribers.</param>
        public Signals(ISignaler<Block> blockConnectedSignaler, ISignaler<Block> blockDisonnectedSignaler, ISignaler<Transaction> transactionSignaler)
        {
            Guard.NotNull(blockConnectedSignaler, nameof(blockConnectedSignaler));
            Guard.NotNull(blockDisonnectedSignaler, nameof(blockDisonnectedSignaler));
            Guard.NotNull(transactionSignaler, nameof(transactionSignaler));

            this.blocksConnected = blockConnectedSignaler;
            this.blocksDisconnected = blockDisonnectedSignaler;
            this.transactions = transactionSignaler;
        }

        /// <summary>Signaler providing notifications about newly available blocks to its subscribers.</summary>
        private ISignaler<Block> blocksConnected { get; }

        /// <summary>Signaler providing notifications about blocks being disconnected to its subscribers.</summary>
        private ISignaler<Block> blocksDisconnected { get; }

        /// <summary>Signaler providing notifications about newly available transactions to its subscribers.</summary>
        private ISignaler<Transaction> transactions { get; }

        /// <inheritdoc />
        public void SignalBlockConnected(Block block)
        {
            Guard.NotNull(block, nameof(block));

            this.blocksConnected.Broadcast(block);
        }

        /// <inheritdoc />
        public void SignalBlockDisconnected(Block block)
        {
            Guard.NotNull(block, nameof(block));

            this.blocksDisconnected.Broadcast(block);
        }

        /// <inheritdoc />
        public void SignalTransaction(Transaction trx)
        {
            Guard.NotNull(trx, nameof(trx));

            this.transactions.Broadcast(trx);
        }

        /// <inheritdoc />
        public IDisposable SubscribeForBlocksConnected(IObserver<Block> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.blocksConnected.Subscribe(observer);
        }

        /// <inheritdoc />
        public IDisposable SubscribeForBlocksDisconnected(IObserver<Block> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.blocksDisconnected.Subscribe(observer);
        }

        /// <inheritdoc />
        public IDisposable SubscribeForTransactions(IObserver<Transaction> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.transactions.Subscribe(observer);
        }
    }
}