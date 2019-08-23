using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Features.SQLiteWalletRepository.External;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Defines the required interface of a wallet repository.
    /// </summary>
    /// <remarks>
    /// The repository should not contain any business logic other than what is implied or provided by services explicitly associated
    /// with this interface. These currently include:
    /// - Network and Consensus. Used for obtaining:
    ///   - the coin type for HdPath resolution
    ///   - human-readable addresses to be returned by some public methods
    /// - DataFolder. Used for obtaining:
    ///   - the wallet folder.
    /// - IDateTimeProvider
    ///   - used for populating CreationTime fields.
    ///   IScriptAddressReader (or IScriptDestinationReader)
    ///   - used to find the destinations of <see cref="TxOut.ScriptPubKey" /> scripts.
    /// </remarks>
    public interface IWalletRepository
    {
        /// <summary>
        /// Updates the relevant wallets from the information contained in the transaction.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="transaction">The transient transaction to process.</param>
        /// <param name="txId">Used to overrides the default transaction id.</param>
        /// <remarks>
        /// This method is intended to be idempotent - i.e. running it twice should not produce any adverse effects.
        /// If any empty wallet addresses have transactions added to them then the affected accounts should
        /// have their addresses topped up to ensure there are always a buffer of unused addresses after the last
        /// address containing transactions.
        /// </remarks>
        void ProcessTransaction(string walletName, Transaction transaction, uint256 txId = null);

        /// <summary>
        /// Updates all the wallets from the information contained in the block.
        /// </summary>
        /// <param name="block">The block to process.</param>
        /// <param name="header">The header of the block passed.</param>
        /// <param name="walletName">Set this to limit processing to the named wallet.</param>
        /// <remarks>
        /// This method is intended to be idempotent - i.e. running it twice consecutively should not produce any adverse effects.
        /// Similar to the rest of the methods it should not contain any business logic other than what may be injected externally.
        /// If any empty wallet addresses have transactions added to them then the affected accounts should
        /// have their addresses topped up to ensure there are always a buffer of unused addresses after the last
        /// address containing transactions.
        /// It's the caller's responsibility to ensure that this method is not called again when it's already executing.
        /// </remarks>
        void ProcessBlock(Block block, ChainedHeader header, string walletName = null);

        /// <summary>
        /// Updates all the wallets from the information contained in the blocks.
        /// </summary>
        /// <param name="blocks">The blocks to process.</param>
        /// <param name="walletName">Set this to limit processing to the named wallet.</param>
        /// <remarks>
        /// This method is intended to be idempotent - i.e. running it twice consecutively should not produce any adverse effects.
        /// Similar to the rest of the methods it should not contain any business logic other than what may be injected externally.
        /// If any empty wallet addresses have transactions added to them then the affected accounts should
        /// have their addresses topped up to ensure there are always a buffer of unused addresses after the last
        /// address containing transactions.
        /// It's the caller's responsibility to ensure that this method is not called again when it's already executing.
        /// </remarks>
        void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null);

        /// <summary>
        /// Initialize an existing or empty database.
        /// </summary>
        /// <param name="dbPerWallet">If set to <c>false</c> then one database will be created for all wallets.</param>
        void Initialize(bool dbPerWallet = true);

        /// <summary>
        /// Creates a wallet without any accounts.
        /// </summary>
        /// <param name="walletName">The name of the wallet to create.</param>
        /// <param name="encryptedSeed">The encrypted seed of the wallet.</param>
        /// <param name="chainCode">The chain code of the walllet.</param>
        /// <param name="lastBlockSynced">The last block synced. Typically the current chain tip or <c>null</c> if the wallet should sync from genesis.</param>
        void CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, ChainedHeader lastBlockSynced = null);

        /// <summary>
        /// Deletes a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet to delete.</param>
        /// <returns>Returns <c>true</c> if the wallet was deleted and <c>false</c> otherwise.</returns>
        bool DeleteWallet(string walletName);

        /// <summary>
        /// Creates a wallet account using a password.
        /// </summary>
        /// <param name="walletName">The name of the wallet to create the account for.</param>
        /// <param name="accountIndex">The account index to create an account for.</param>
        /// <param name="accountName">The account name to use.</param>
        /// <param name="password">The wallet password for use to decrypt information used to generate new address pubkeys.</param>
        /// <param name="creationTime">Used to override the default creation time of the account.</param>
        void CreateAccount(string walletName, int accountIndex, string accountName, string password, DateTimeOffset? creationTime = null);

        /// <summary>
        /// Creates a wallet account using an extended public key.
        /// </summary>
        /// <param name="walletName">The name of the wallet to create the account for.</param>
        /// <param name="accountIndex">The account index to create an account for.</param>
        /// <param name="accountName">The account name to use.</param>
        /// <param name="extPubKey">The extended public key for the account.</param>
        /// <param name="creationTime">Used to override the default creation time of the account.</param>
        void CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey, DateTimeOffset? creationTime = null);

        /// <summary>
        /// Gets up to the specified number of unused addresses.
        /// </summary>
        /// <param name="accountReference">The account to get unused addresses for.</param>
        /// <param name="count">The maximum number of addresses to return.</param>
        /// <param name="isChange">The type of addresses to return.</param>
        /// <returns>A list of unused addresses.</returns>
        IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false);

        /// <summary>
        /// Gets all spendable transactions in the wallet with the given number of confirmation.
        /// </summary>
        /// <param name="accountReference">The account to get unused addresses for.</param>
        /// <param name="currentChainHeight">The chain height to use in the determination of the number of confirmations of a transaction. </param>
        /// <param name="confirmations">The minimum number of confirmations for a transactions to be regarded spendable.</param>
        /// <returns>The list of spendable transactions for the account.</returns>
        /// <remarks>For coinbase transactions <see cref="Network.Consensus.CoinbaseMaturity" /> will be used in addition to <paramref name="confirmations"/>.</remarks>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference accountReference, int currentChainHeight, int confirmations = 0);

        /// <summary>
        /// Returns a history of all transactions in the wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet to return the transactions of.</param>
        /// <param name="accountName">An optional account name to limit the results to a particular account.</param>
        /// <returns>A history of all transactions in the wallet.</returns>
        IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null);

        /// <summary>
        /// Allows an unconfirmed transaction to be removed.
        /// </summary>
        /// <param name="walletName">The name of the wallet to return the transactions of.</param>
        /// <param name="txId">The transaction id of the transaction to remove.</param>
        void RemoveUnconfirmedTransaction(string walletName, uint256 txId);

        /// <summary>
        /// Determines a block in common between the supplied chain tip and the wallet block locator.
        /// </summary>
        /// <param name="walletName">The name of the wallet to determine the fork for.</param>
        /// <param name="chainTip">The chain tip to use in determining the fork.</param>
        /// <returns>The fork or <c>null</c> if there are no blocks in common.</returns>
        ChainedHeader FindFork(string walletName, ChainedHeader chainTip);

        /// <summary>
        /// Only keep wallet transactions up to and including the specified block.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="lastBlockSynced">The last block synced to set.</param>
        /// <remarks>The value of lastBlockSynced must match a block that was conceivably processed by the wallet (or be null).</remarks>
        void RewindWallet(string walletName, ChainedHeader lastBlockSynced);

        /// <summary>
        /// Allows multiple interface calls to be grouped into a transaction.
        /// </summary>
        /// <param name="walletName">The wallet the transaction is for.</param>
        /// <returns>A transaction context providing <see cref="ITransactionContext.Commit"/> and <see cref="ITransactionContext.Rollback"/> methods.</returns>
        ITransactionContext BeginTransaction(string walletName);
    }
}
