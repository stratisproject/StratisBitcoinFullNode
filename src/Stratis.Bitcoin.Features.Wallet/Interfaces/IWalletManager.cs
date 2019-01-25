using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BuilderExtensions;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IWalletManager
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
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0);

        /// <summary>
        /// Lists all spendable transactions from the accounts in the wallet participating in staking.
        /// </summary>
        /// <returns>A collection of spendable outputs</returns>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWalletForStaking(string walletName, int confirmations = 0);

        /// <summary>
        /// Helps identify UTXO's that can participate in staking.
        /// </summary>
        /// <returns>A dictionary containing string and template pairs - e.g. { "P2PK", PayToPubkeyTemplate.Instance }</returns>
        Dictionary<string, ScriptTemplate> GetValidStakingTemplates();

        /// <summary>
        /// Returns additional transaction builder extensions to use for building staking transactions.
        /// </summary>
        /// <returns>Transaction builder extensions to use for building staking transactions.</returns>
        IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking();

        /// <summary>
        /// Lists all spendable transactions from the account specified in <see cref="WalletAccountReference"/>.
        /// </summary>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0);

        /// <summary>
        /// Creates a wallet and persist it as a file on the local system.
        /// </summary>
        /// <param name="password">The password used to encrypt sensitive info.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>
        /// <returns>A mnemonic defining the wallet's seed used to generate addresses.</returns>
        Mnemonic CreateWallet(string password, string name, string passphrase = null, Mnemonic mnemonic = null);

        /// <summary>
        /// Loads a wallet from a file.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <returns>The wallet.</returns>
        Wallet LoadWallet(string password, string name);

        /// <summary>
        /// Recovers a wallet using mnemonic and password.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <returns>The recovered wallet.</returns>
        Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null);

        /// <summary>
        /// Recovers a wallet using extended public key and account index.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="extPubKey">The extended public key.</param>
        /// <param name="accountIndex">The account number.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <returns></returns>
        Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime);

        /// <summary>
        /// Deletes a wallet.
        /// </summary>
        void DeleteWallet();

        /// <summary>
        /// Gets an account that contains no transactions.
        /// </summary>
        /// <param name="walletName">The name of the wallet from which to get an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>An unused account.</returns>
        HdAccount GetUnusedAccount(string walletName, string password);

        /// <summary>
        /// Gets an account that contains no transactions.
        /// </summary>
        /// <param name="wallet">The wallet from which to get an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>An unused account.</returns>
        HdAccount GetUnusedAccount(Wallet wallet, string password);

        /// <summary>
        /// Gets an address that contains no transaction.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account</param>
        /// <returns>An unused address or a newly created address, in Base58 format.</returns>
        HdAddress GetUnusedAddress(WalletAccountReference accountReference);

        /// <summary>
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <returns>An unused change address or a newly created change address, in Base58 format.</returns>
        HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference);

        /// <summary>
        /// Gets a collection of unused receiving or change addresses.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account.</param>
        /// <param name="count">The number of addresses to create.</param>
        /// <param name="isChange">A value indicating whether or not the addresses to get should be receiving or change addresses.</param>
        /// <returns>A list of unused addresses. New addresses will be created as necessary.</returns>
        IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false);

        /// <summary>
        /// Gets the history of transactions contained in an account.
        /// If no account name is specified, history will be returned for all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accountName">The account name.</param>
        /// <returns>Collection of address history and transaction pairs.</returns>
        IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null);

        /// <summary>
        /// Gets the history of the transactions in addresses contained in this account.
        /// </summary>
        /// <param name="account">The account for which to get history.</param>
        /// <returns>The history for this account.</returns>
        AccountHistory GetHistory(HdAccount account);

        /// <summary>
        /// Gets the balance of transactions contained in an account.
        /// If no account name is specified, balances will be returned for all accounts in the wallet.
        /// </summary>
        /// <param name="walletName">The wallet name.</param>
        /// <param name="accountName">The account name.</param>
        /// <returns>Collection of account balances.</returns>
        IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null);

        /// <summary>
        /// Gets the balance of transactions for this specific address.
        /// </summary>
        /// <param name="address">The address to get the balance from.</param>
        /// <returns>The address balance for an address.</returns>
        AddressBalance GetAddressBalance(string address);

        /// <summary>
        /// Gets some general information about a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <returns></returns>
        Wallet GetWallet(string walletName);

        /// <summary>
        /// Gets a list of accounts.
        /// </summary>
        /// <param name="walletName">The name of the wallet to look into.</param>
        /// <returns></returns>
        IEnumerable<HdAccount> GetAccounts(string walletName);

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
        /// <param name="chainedHeader">The blocks chain of headers.</param>
        void ProcessBlock(Block block, ChainedHeader chainedHeader);

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
        /// <param name="wallet">The wallet to save.</param>
        void SaveWallet(Wallet wallet);

        /// <summary>
        /// Saves all the loaded wallets into the file system.
        /// </summary>
        void SaveWallets();

        /// <summary>
        /// Gets the extension of the wallet files.
        /// </summary>
        /// <returns></returns>
        string GetWalletFileExtension();

        /// <summary>
        /// Gets all the wallets' names.
        /// </summary>
        /// <returns>A collection of the wallets' names.</returns>
        IEnumerable<string> GetWalletsNames();

        /// <summary>
        /// Updates the wallet with the height of the last block synced.
        /// </summary>
        /// <param name="wallet">The wallet to update.</param>
        /// <param name="chainedHeader">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedHeader chainedHeader);

        /// <summary>
        /// Updates all the loaded wallets with the height of the last block synced.
        /// </summary>
        /// <param name="chainedHeader">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader);

        /// <summary>
        /// Gets a wallet given its name.
        /// </summary>
        /// <param name="walletName">The name of the wallet to get.</param>
        /// <returns>A wallet or null if it doesn't exist</returns>
        Wallet GetWalletByName(string walletName);

        /// <summary>
        /// Gets the block locator of the first loaded wallet.
        /// </summary>
        /// <returns></returns>
        ICollection<uint256> GetFirstWalletBlockLocator();

        /// <summary>
        /// Gets the list of the wallet filenames, along with the folder in which they're contained.
        /// </summary>
        /// <returns>The wallet filenames, along with the folder in which they're contained.</returns>
        (string folderPath, IEnumerable<string>) GetWalletsFiles();

        /// <summary>
        /// Gets whether there are any wallet files loaded or not.
        /// </summary>
        /// <returns>Whether any wallet files are loaded.</returns>
        bool ContainsWallets { get; }

        /// <summary>
        /// Gets the extended public key of an account.
        /// </summary>
        /// <param name="accountReference">The account.</param>
        /// <returns>The extended public key.</returns>
        string GetExtPubKey(WalletAccountReference accountReference);

        /// <summary>
        /// Gets the lowest LastBlockSyncedHeight of all loaded wallet account roots.
        /// </summary>
        /// <returns>The lowest LastBlockSyncedHeight or null if there are no account roots yet.</returns>
        int? GetEarliestWalletHeight();

        /// <summary>
        /// Gets the oldest wallet creation time.
        /// </summary>
        /// <returns></returns>
        DateTimeOffset GetOldestWalletCreationTime();

        /// <summary>
        /// Removes the specified transactions from the wallet and persist it.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <param name="transactionsIds">The IDs of transactions to remove.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIdsLocked(string walletName, IEnumerable<uint256> transactionsIds);

        /// <summary>
        /// Removes all the transactions from the wallet and persist it.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName);

        /// <summary>
        /// Removes all the transactions that occurred after a certain date.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <param name="fromDate">The date after which the transactions should be removed.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate);
    }
}
