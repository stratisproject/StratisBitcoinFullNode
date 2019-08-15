using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

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
    ///   IScriptPubKeyProvider
    ///   - used by CreateAccount to generate ScriptPubKeys for addresses.
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
        /// have their addresses topped up to ensure there are always 20 unused addresses after the last
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
        /// This method is intended to be idempotent - i.e. running it twice should not produce any adverse effects.
        /// Similar to the rest of the methods it should not contain any business logic other than what may be injected externally.
        /// If any empty wallet addresses have transactions added to them then the affected accounts should
        /// have their addresses topped up to ensure there are always 20 unused addresses after the last
        /// address containing transactions.
        /// </remarks>
        void ProcessBlock(Block block, ChainedHeader header, string walletName = null);

        /// <summary>
        /// Initialize an existing or empty database.
        /// </summary>
        /// <param name="seperateWallets">If set the repository will split the wallets into separate files.</param>
        void Initialize(bool seperateWallets = true);

        /// <summary>
        /// Creates a wallet without any accounts.
        /// </summary>
        /// <param name="walletName">The name of the wallet to create.</param>
        /// <param name="encryptedSeed">The encrypted seed of the wallet.</param>
        /// <param name="chainCode">The chain code of the walllet.</param>
        /// <param name="lastBlockSynced">The last block synced. Typically the current chain tip or <c>null</c> if the wallet should sync from genesis.</param>
        void CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, ChainedHeader lastBlockSynced = null);

        /// <summary>
        /// Creates a wallet account.
        /// </summary>
        /// <param name="walletName">The name of the wallet to create the account for.</param>
        /// <param name="accountIndex">The account index to create an account for.</param>
        /// <param name="accountName">The account name to use.</param>
        /// <param name="password">The wallet password for use to decrypt information used to generate new address pubkeys.</param>
        /// <param name="scriptPubKeyType">Used to generate 20 unused wallet addresses. If <c>null</c> then no addresses are generated.</param>
        /// <param name="creationTime">Used to override the default creation time of the account.</param>
        void CreateAccount(string walletName, int accountIndex, string accountName, string password, string scriptPubKeyType, DateTimeOffset? creationTime = null);

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
        /// <param name="chainTip">The chain tip to use in the determination of the number of confirmations of a transaction. </param>
        /// <param name="confirmations">The minimum number of confirmations for a transactions to be regarded spendable.</param>
        /// <returns>The list of spendable transactions for the account.</returns>
        /// <remarks>For coinbase transactions <see cref="Network.Consensus.CoinbaseMaturity" /> will be used in addition to <paramref name="confirmations"/>.</remarks>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference accountReference, ChainedHeader chainTip, int confirmations = 0);

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
        /// Only keep wallet transactions up to and including the specified block.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="lastBlockSynced">The last block synced to set.</param>
        /// <remarks>The value of lastBlockSynced must match a block that was conceivably processed by the wallet (or be null).</remarks>
        void RewindWallet(string walletName, ChainedHeader lastBlockSynced);
    }
}
