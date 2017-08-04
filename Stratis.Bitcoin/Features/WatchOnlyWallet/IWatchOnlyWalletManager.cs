using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    /// <summary>
    /// An interface representing a manager providing operations on a watch-only wallet.
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
        WatchOnlyWallet GetWallet();

        /// <summary>
        /// Gets the last processed block.
        /// </summary>
        uint256 LastReceivedBlock { get; }

        /// <summary>
        /// Adds this <see cref="Script"/> to the watch-only wallet so that transactions including it will be monitored.
        /// </summary>
        /// <param name="script">The script to watch for in transactions.</param>
        void Watch(Script script);

        /// <summary>
        /// Removes all the thransactions in the wallet that are above this block height.
        /// </summary>
        void RemoveBlocks(ChainedBlock fork);
        
        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block to process.</param>
        void ProcessBlock(Block block);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction to process.</param>
        /// <param name="blockHeight">The height of the block this transaction came from. <c>null</c> if it was not a transaction included in a block.</param>
        /// <param name="block">The block in which this transaction was included. <c>null</c> if it was not a transaction included in a block.</param>
        void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null);
        
        /// <summary>
        /// Saves the watch-only wallet to a persistent storage.
        /// </summary>
        void SaveWatchOnlyWallet();

        /// <summary>
        /// Loads a stored watch-only wallet.
        /// </summary>
        /// <returns>The stored watch-only wallet.</returns>
        WatchOnlyWallet LoadWatchOnlyWallet();

        /// <summary>
        /// Updates all the watch-only wallet with the height of the last block synced.
        /// </summary>
        /// <param name="chainedBlock">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock);
    }
}
