using System.Collections.Generic;
using NBitcoin;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using FlatHistory = Stratis.Bitcoin.Features.Wallet.FlatHistory;
using UnspentOutputReference = Stratis.Bitcoin.Features.Wallet.UnspentOutputReference;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IFederationWalletManager
    {
        /// <summary>
        /// Starts this wallet manager.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the wallet manager.
        /// <para>Internally it waits for async loops to complete before saving the wallets to disk.</para>
        /// </summary>
        void Stop();

        /// <summary>
        /// The last processed block.
        /// </summary>
        uint256 WalletTipHash { get; set; }

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <returns>A collection of spendable outputs</returns>
        IEnumerable<Wallet.UnspentOutputReference> GetSpendableTransactionsInWallet(int confirmations = 0);

        /// <summary>
        /// Gets the last block height.
        /// </summary>
        /// <returns></returns>
        int LastBlockHeight();

        /// <summary>
        /// Remove all the transactions in the wallet that are above this block height
        /// </summary>
        void RemoveBlocks(ChainedHeader fork);

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="chainedBlock">The blocks chain of headers.</param>
        void ProcessBlock(Block block, ChainedHeader chainedBlock);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="blockHeight">The height of the block this transaction came from. Null if it was not a transaction included in a block.</param>
        /// <param name="block">The block in which this transaction was included.</param>
        /// <param name="isPropagated">Transaction propagation state.</param>
        /// <returns>A value indicating whether this transaction affects the wallet.</returns>
        bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true);

        /// <summary>
        /// Saves the wallet into the file system.
        /// </summary>
        void SaveWallet();

        /// <summary>
        /// Gets some general information about a wallet.
        /// </summary>
        /// <returns></returns>
        FederationWallet GetWallet();

        /// <summary>
        /// Updates the wallet with the height of the last block synced.
        /// </summary>
        /// <param name="wallet">The wallet to update.</param>
        /// <param name="chainedBlock">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(ChainedHeader chainedBlock);

        /// <summary>
        /// Gets whether there are any wallet files loaded or not.
        /// </summary>
        /// <returns>Whether any wallet files are loaded.</returns>
        bool ContainsWallets { get; }

        WalletSecret Secret { get; set; }

        /// <summary>
        /// Imports the federation member's mnemonic key.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="mnemonic">The user's mnemonic.</param>
        void ImportMemberKey(string password, string mnemonic);
    }
}
