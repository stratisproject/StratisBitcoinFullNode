using System;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    public interface IFederationWalletSyncManager
    {
        /// <summary>
        /// Starts the walletSyncManager.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the walletSyncManager.
        /// <para>
        /// We need to call <see cref="Stop"/> explicitly to check that the internal async loop isn't still running
        /// and subsequentlly dispose of it properly.
        /// </para>
        /// </summary>
        void Stop();

        /// <summary>
        /// Processes a new block.
        /// </summary>
        /// <param name="block"></param>
        void ProcessBlock(Block block);

        /// <summary>
        /// Processes a new transaction which is in a pending state (not included in a block).
        /// </summary>
        /// <param name="transaction"></param>
        void ProcessTransaction(Transaction transaction);

        /// <summary>
        /// Synchronize the wallet starting from the date passed as a parameter.
        /// </summary>
        /// <param name="date">The date from which to start the sync process.</param>
        /// <returns>The height of the block sync will start from.</returns>
        void SyncFromDate(DateTime date);

        /// <summary>
        /// Synchronize the wallet starting from the height passed as a parameter.
        /// </summary>
        /// <param name="height">The height from which to start the sync process.</param>
        /// <returns>The height of the block sync will start from.</returns>
        void SyncFromHeight(int height);

        /// <summary>
        /// The current tip of the wallet.
        /// </summary>
        ChainedHeader WalletTip { get; }
    }
}
