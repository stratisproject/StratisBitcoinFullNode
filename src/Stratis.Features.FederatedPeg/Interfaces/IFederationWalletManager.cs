using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IFederationWalletManager : ILockProtected
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
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(int confirmations = 0);

        /// <summary>
        /// Gets the hash of the last block received by the wallet.
        /// </summary>
        /// <returns>Hash height pair of the last block received by the wallet.</returns>
        HashHeightPair LastBlockSyncedHashHeight();

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
        /// <param name="blockHash">The hash of the block this transaction came from. Null if it was not a transaction included in a block.</param>
        /// <param name="block">The block in which this transaction was included.</param>
        /// <returns>A value indicating whether this transaction affects the wallet.</returns>
        bool ProcessTransaction(Transaction transaction, int? blockHeight = null, uint256 blockHash = null, Block block = null);

        /// <summary>
        /// Verifies that the transaction's input UTXO's have been reserved by the wallet.
        /// Also checks that an earlier transaction for the same deposit id does not exist.
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        /// <param name="checkSignature">Indicates whether to check the signature.</param>
        /// <returns><c>True</c> if all's well and <c>false</c> otherwise.</returns>
        bool ValidateTransaction(Transaction transaction, bool checkSignature = false);

        /// <summary>
        /// Verifies that a transaction's inputs aren't being consumed by any other transactions.
        /// </summary>
        /// <param name="transaction">The transaction to check.</param>
        /// <param name="checkSignature">Indicates whether to check the signature.</param>
        /// <returns><c>True</c> if all's well and <c>false</c> otherwise.</returns>
        bool ValidateConsolidatingTransaction(Transaction transaction, bool checkSignature = false);

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
        /// Finds all withdrawal transactions with optional filtering by deposit id or transaction id.
        /// </summary>
        /// <param name="depositId">Filters by this deposit id if not <c>null</c>.</param>
        /// <param name="sort">Sorts the results ascending according to earliest input UTXO.</param>
        /// <returns>The transaction data containing the withdrawal transaction.</returns>
        List<(Transaction, IWithdrawal)> FindWithdrawalTransactions(uint256 depositId = null, bool sort = false);

        /// <summary>
        /// Removes the withdrawal transaction(s) associated with the corresponding deposit id.
        /// </summary>
        /// <param name="depositId">The deposit id identifying the withdrawal transaction(s) to remove.</param>
        bool RemoveWithdrawalTransactions(uint256 depositId);

        /// <summary>
        /// Removes transaction data that is still to be confirmed in a block.
        /// </summary>
        bool RemoveUnconfirmedTransactionData();

        /// <summary>
        /// Determines if federation has been activated.
        /// </summary>
        /// <returns><c>True</c> if federation is active and <c>false</c> otherwise.</returns>
        bool IsFederationWalletActive();

        /// <summary>
        /// Enables federation.
        /// </summary>
        /// <param name="password">The federation wallet password.</param>
        /// <param name="mnemonic">The user's mnemonic.</param>
        /// <param name="passphrase">A passphrase used to derive the private key from the mnemonic.</param>
        void EnableFederationWallet(string password, string mnemonic = null, string passphrase = null);

        /// <summary>
        /// Removes all the transactions from the federation wallet.
        /// </summary>
        /// <returns>A list of objects made up of transaction IDs along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions();

        /// <summary>
        /// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        /// <returns>A tuple containing the confirmed and unconfirmed balances.</returns>
        (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount();
    }
}
