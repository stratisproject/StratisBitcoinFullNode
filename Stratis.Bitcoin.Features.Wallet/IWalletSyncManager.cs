using System;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
	public interface IWalletSyncManager
	{
		/// <summary>
		/// Initializes the walletSyncManager.
		/// </summary>
		/// <returns></returns>
		Task Initialize();

		/// <summary>
		/// Processes a new block
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
		/// <returns>The height of the block sync will start from</returns>
		void SyncFrom(DateTime date);

		/// <summary>
		/// Synchronize the wallet starting from the height passed as a parameter.
		/// </summary>
		/// <param name="height">The height from which to start the sync process.</param>
		/// <returns>The height of the block sync will start from</returns>
		void SyncFrom(int height);

        /// <summary>
        /// The current tip of the wallet.
        /// </summary>
        ChainedBlock WalletTip { get; }
	}
}
