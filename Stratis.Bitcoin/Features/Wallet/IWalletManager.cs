using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IWalletManager : IDisposable
    {
        /// <summary>
        /// Initializes this wallet manager.
        /// </summary>
        void Initialize();

        /// <summary>
        /// The last processed block.
        /// </summary>
        uint256 WalletTipHash { get; }

        /// <summary>
        /// List all spendable transactions from all accounts
        /// </summary>
        /// <returns>A collection of spendable outputs</returns>
        List<UnspentInfo> GetSpendableTransactions(int confirmations = 0);

        /// <summary>
        /// Creates a wallet and persist it as a file on the local system.
        /// </summary>
        /// <param name="password">The password used to encrypt sensitive info.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>		
        /// <returns>A mnemonic defining the wallet's seed used to generate addresses.</returns>
        Mnemonic CreateWallet(string password, string name, string passphrase = null, string mnemonic = null);

        /// <summary>
        /// Loads a wallet from a file.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <returns>The wallet.</returns>
        Wallet LoadWallet(string password, string name);

        /// <summary>
        /// Recovers a wallet.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="name">The name of the wallet.</param>
        /// <param name="mnemonic">The user's mnemonic for the wallet.</param>		
        /// <param name="passphrase">The passphrase used in the seed.</param>
        /// <param name="creationTime">The date and time this wallet was created.</param>
        /// <returns>The recovered wallet.</returns>
        Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null);

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
        /// Creates a new account.
        /// </summary>
        /// <param name="wallet">The wallet in which this account will be created.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>The new account.</returns>
        HdAccount CreateNewAccount(Wallet wallet, string password);

        /// <summary>
        /// Gets an address that contains no transaction.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account</param>
        /// <returns>An unused address or a newly created address, in Base58 format.</returns>
        HdAddress GetUnusedAddress(WalletAccountReference accountReference);

        /// <summary>
        /// Gets a collection of addresses containing transactions for this coin.
        /// </summary>
        /// <param name="walletName">The name of the wallet to get history from.</param>
        /// <returns></returns>
        IEnumerable<HdAddress> GetHistory(string walletName);

        /// <summary>
        /// Gets a collection of addresses containing transactions for this coin.
        /// </summary>
        /// <param name="wallet">The wallet to get history from.</param>
        /// <returns></returns>
        IEnumerable<HdAddress> GetHistory(Wallet wallet);

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
        /// Builds a transaction to be sent to the network.
        /// </summary>
        /// <param name="accountReference">The name of the wallet and account</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <param name="destinationScript">The destination to send the funds to.</param>
        /// <param name="amount">The amount of funds to be sent.</param>
        /// <param name="feeType">The type of fee to be included.</param>
        /// <param name="minConfirmations">The minimum number of confirmations we require for unspent outputs to be included.</param>
        /// <returns></returns>
        (string hex, uint256 transactionId, Money fee) BuildTransaction(WalletAccountReference accountReference, string password, Script destinationScript, Money amount, FeeType feeType, int minConfirmations);

        /// <summary>
        /// Remove all the thransactions in the wallet that are above this block height
        /// </summary>
        void RemoveBlocks(ChainedBlock fork);

        /// <summary>
        /// Sends a transaction to the network.
        /// </summary>
        /// <param name="transactionHex">The hex of the transaction.</param>
        /// <returns></returns>
        bool SendTransaction(string transactionHex);

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="chainedBlock">The blocks chain of headers.</param>
        void ProcessBlock(Block block, ChainedBlock chainedBlock);

        /// <summary>
        /// Processes a transaction received from the network.
        /// </summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="blockHeight">The height of the block this transaction came from. Null if it was not a transaction included in a block.</param>
        /// <param name="block">The block in which this transaction was included.</param>
        void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null);

        /// <summary>
        /// Saves the wallet into the file system.
        /// </summary>
        /// <param name="wallet">The wallet to save.</param>
        void SaveToFile(Wallet wallet);

        /// <summary>
        /// Saves all the loaded wallets into the file system.
        /// </summary>        
        void SaveToFile();

        /// <summary>
        /// Gets the extension of the wallet files.
        /// </summary>
        /// <returns></returns>
        string GetWalletFileExtension();

        /// <summary>
        /// Get all the wallets name
        /// </summary>
        /// <returns></returns>
        string[] GetWallets();
        
        /// <summary>
        /// Updates the wallet with the height of the last block synced.
        /// </summary>
        /// <param name="wallet">The wallet to update.</param>
        /// <param name="chainedBlock">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedBlock chainedBlock);

        /// <summary>
        /// Updates all the loaded wallets with the height of the last block synced.
        /// </summary>
        /// <param name="chainedBlock">The height of the last block synced.</param>
        void UpdateLastBlockSyncedHeight(ChainedBlock chainedBlock);
    }
}