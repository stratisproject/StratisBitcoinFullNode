using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    /// <summary>
    /// An interface representing a manager providing operations on a watch-only wallet.
    /// TODO Add filtering (and clearing) of transactions whose block hash is not found on the chain (meaning a reorg occurred).
    /// </summary>
    public interface IWatchOnlyWalletManager : IDisposable
    {
        /// <summary>
        /// Initializes this watch-only wallet manager.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Gets the watch-only wallet.
        /// </summary>
        /// <returns>A watch-only wallet.</returns>
        WatchOnlyWallet GetWatchOnlyWallet();

        /// <summary>
        /// Adds this scriptPubKey to the watch-only wallet so that transactions affecting it will be monitored.
        /// </summary>
        /// <param name="scriptPubKey">The scriptPubKey.</param>
        void WatchScriptPubKey(Script scriptPubKey);

        /// <summary>
        /// Adds this base58 encoded address to the watch-only wallet so that transactions affecting it will be monitored.
        /// </summary>
        /// <param name="address">The base58 address to watch for in transactions.</param>
        void WatchAddress(string address);

        /// <summary>
        /// Stores a transaction.
        /// </summary>
        /// <param name="transactionData">The transaction data.</param>
        void StoreTransaction(TransactionData transactionData);

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block to process.</param>
        void ProcessBlock(Block block);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction to process.</param>
        /// <param name="block">The block in which this transaction was included. <c>null</c> if it was not a transaction included in a block.</param>
        void ProcessTransaction(Transaction transaction, Block block = null);

        /// <summary>
        /// Saves the watch-only wallet to a persistent storage.
        /// </summary>
        void SaveWatchOnlyWallet();

        /// <summary>
        /// Loads a stored watch-only wallet.
        /// </summary>
        /// <returns>The stored watch-only wallet.</returns>
        WatchOnlyWallet LoadWatchOnlyWallet();
    }
}
