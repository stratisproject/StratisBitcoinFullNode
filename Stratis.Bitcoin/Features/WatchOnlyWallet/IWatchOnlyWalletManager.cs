using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IWatchOnlyWalletManager : IDisposable
    {
        /// <summary>
        /// Initializes this wallet manager.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Gets the wallet.
        /// </summary>
        /// <returns></returns>
        WatchOnlyWallet GetWallet();

        /// <summary>
        /// The last processed block.
        /// </summary>
        uint256 LastReceivedBlock { get; }

        /// <summary>
        /// Watch for transactions including this Script.
        /// </summary>
        /// <param name="script"></param>
        void Watch(Script script);

        /// <summary>
        /// Remove all the thransactions in the wallet that are above this block height
        /// </summary>
        void RemoveBlocks(ChainedBlock fork);
        
        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block.</param>
        void ProcessBlock(Block block);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="blockHeight">The height of the block this transaction came from. Null if it was not a transaction included in a block.</param>
        /// <param name="block">The block in which this transaction was included.</param>
        void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null);
        
        /// <summary>
        /// Saves all the loaded wallets into the file system.
        /// </summary>        
        void SaveToFile();
        
        /// <summary>
        /// Updates all the loaded wallets with the height of the last block synced.
        /// </summary>
        /// <param name="chainedBlock">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock);
    }
}
