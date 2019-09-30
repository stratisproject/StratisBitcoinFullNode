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
        uint256 WalletTipHash { get; }

        /// <summary>
        /// The last processed block height.
        /// </summary>
        int WalletTipHeight { get; }

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
        (Wallet, Mnemonic) CreateWallet(string password, string name, string passphrase = null, Mnemonic mnemonic = null);

        /// <summary>
        /// Signs a string message.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="externalAddress">Address to use to sign.</param>
        /// <param name="message">Message to sign.</param>
        /// <returns>The generated signature.</returns>
        string SignMessage(string password, string walletName, string externalAddress, string message);

        /// <summary>
        /// Verifies the signed message.
        /// </summary>
        /// <param name="externalAddress">Address used to sign.</param>
        /// <param name="message">Message to verify.</param>
        /// <param name="signature">Message signature.</param>
        /// <returns>True if the signature is valid, false if it is invalid.</returns>
        bool VerifySignedMessage(string externalAddress, string message, string signature);

        /// <summary>
        /// Checks the wallet password.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <returns>The wallet.</returns>
        Wallet LoadWallet(string password, string name);

        /// <summary>
        /// Unlocks a wallet for the specified time.
        /// </summary>
        /// <param name="password">The wallet password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="timeout">The timeout in seconds.</param>
        void UnlockWallet(string password, string name, int timeout);

        /// <summary>
        /// Locks the wallet.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        void LockWallet(string name);

        /// <summary>
        /// Recovers a wallet using mnemonic and password.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <returns>The recovered wallet.</returns>
        Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null, ChainedHeader lastBlockSynced = null);

        /// <summary>
        /// Recovers a wallet using extended public key and account index.
        /// </summary>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="extPubKey">The extended public key.</param>
        /// <param name="accountIndex">The account number.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <returns></returns>
        Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime, ChainedHeader lastBlockSynced = null);

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
        void ProcessBlock(Block block, ChainedHeader chainedHeader = null);

        void ProcessBlocks(Func<int, IEnumerable<(ChainedHeader, Block)>> blockProvider);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <returns>A value indicating whether this transaction affects the wallet.</returns>
        bool ProcessTransaction(Transaction transaction);

        /// <summary>
        /// Saves the wallet into the file system.
        /// </summary>
        /// <param name="wallet">The wallet to save.</param>
        void SaveWallet(string wallet);

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
        /// Gets a wallet given its name.
        /// </summary>
        /// <param name="walletName">The name of the wallet to get.</param>
        /// <returns>A wallet or null if it doesn't exist</returns>
        Wallet GetWallet(string walletName);

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
        /// Gets the extended private key of an account.
        /// </summary>
        /// <param name="accountReference">The account.</param>
        /// <param name="password">The password used to decrypt the encrypted seed.</param>
        /// <param name="cache">whether to cache the private key for future use.</param>
        /// <returns>The private key.</returns>
        ExtKey GetExtKey(WalletAccountReference accountReference, string password = "", bool cache = false);

        /// <summary>
        /// Removes the specified transactions from the wallet and persist it.
        /// </summary>
        /// <param name="walletName">The name of the wallet to remove transactions from.</param>
        /// <param name="transactionsIds">The IDs of transactions to remove.</param>
        /// <returns>A list of objects made up of a transactions ID along with the time at which they were created.</returns>
        HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds);

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

        /// <summary>
        /// Finds the fork point between the wallet and the passed header.
        /// </summary>
        /// <param name="walletName">The wallet name to identify the fork for.</param>
        /// <param name="chainedHeader">The header to use in determining the fork.</param>
        /// <returns>Returns the last block in common betweeen the wallet and the chain identified by the passed header.</returns>
        ChainedHeader FindFork(string walletName, ChainedHeader chainedHeader);

        /// <summary>
        /// Finds the highest common wallet tip on the consensus chain.
        /// </summary>
        /// <param name="consensusTip">Identifies the tip of the consensus chain.</param>
        /// <returns>The highest common wallet tip on the consensus chain.</returns>
        ChainedHeader WalletCommonTip(ChainedHeader consensusTip);

        /// <summary>
        /// Rewind a wallet to the fork point identified via the supplied header.
        /// </summary>
        /// <param name="walletName">The name of the wallet to rewind.</param>
        /// <param name="chainedHeader">The header to use in determining the fork.</param>
        void RewindWallet(string walletName, ChainedHeader chainedHeader);

        IWalletRepository WalletRepository { get; }

        int GetAddressBufferSize();

        /// <summary>
        /// Returns a list of grouped addresses which have had their common ownership made public by common use as inputs or as the resulting change in past transactions.
        /// </summary
        /// <remarks>
        /// Please see https://github.com/bitcoin/bitcoin/blob/726d0668ff780acb59ab0200359488ce700f6ae6/src/wallet/wallet.cpp#L3641
        /// </remarks>
        /// <param name="walletName">The wallet in question.</param>
        /// <returns>The grouped list of base58 addresses.</returns>
        IEnumerable<IEnumerable<string>> GetAddressGroupings(string walletName);

        /// <summary>
        /// Syncs the wallets from a specific height.
        /// </summary>
        /// <param name="tip">Identifies the height to sync from.</param>
        /// <param name="walletName">The optional wallet name if only a specific wallet should be synced.</param>
        void UpdateLastBlockSyncedHeight(ChainedHeader tip, string walletName = null);
    }
}
