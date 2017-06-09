using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Wallet
{
    /// <summary>
    /// Interface for a manager providing operations on wallets.
    /// </summary>
    public interface IWalletManager : IDisposable
    {
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
		/// <returns>A mnemonic defining the wallet's seed used to generate addresses.</returns>
		Mnemonic CreateWallet(string password, string name, string passphrase = null);

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
        /// <param name="coinType">The type of coin for which to get an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>An unused account.</returns>
        HdAccount GetUnusedAccount(string walletName, CoinType coinType, string password);

        /// <summary>
        /// Gets an account that contains no transactions.
        /// </summary>
        /// <param name="wallet">The wallet from which to get an account.</param>
        /// <param name="coinType">The type of coin for which to get an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>An unused account.</returns>
        HdAccount GetUnusedAccount(Wallet wallet, CoinType coinType, string password);

        /// <summary>
        /// Creates a new account.
        /// </summary>
        /// <param name="wallet">The wallet in which this account will be created.</param>
        /// <param name="coinType">The type of coin for which to create an account.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        /// <returns>The new account.</returns>
        HdAccount CreateNewAccount(Wallet wallet, CoinType coinType, string password);

		/// <summary>
		/// Gets an address that contains no transaction.
		/// </summary>
		/// <param name="walletName">The name of the wallet in which this address is contained.</param>
		/// <param name="coinType">The type of coin for which to get the address.</param>
		/// <param name="accountName">The name of the account in which this address is contained.</param>
		/// <returns>An unused address or a newly created address, in Base58 format.</returns>
		HdAddress GetUnusedAddress(string walletName, string accountName);

        /// <summary>
        /// Gets a collection of addresses containing transactions for this coin.
        /// </summary>
        /// <param name="walletName">The name of the wallet to get history from.</param>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        IEnumerable<HdAddress> GetHistoryByCoinType(string walletName, CoinType coinType);

        /// <summary>
        /// Gets a collection of addresses containing transactions for this coin.
        /// </summary>
        /// <param name="wallet">The wallet to get history from.</param>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        IEnumerable<HdAddress> GetHistoryByCoinType(Wallet wallet, CoinType coinType);

        /// <summary>
        /// Gets some general information about a wallet.
        /// </summary>
        /// <param name="walletName">The name of the wallet.</param>
        /// <returns></returns>
        Wallet GetWallet(string walletName);

        /// <summary>
        /// Gets a list of accounts filtered by coin type.
        /// </summary>
        /// <param name="walletName">The name of the wallet to look into.</param>
        /// <param name="coinType">The type of coin to filter by.</param>
        /// <returns></returns>
        IEnumerable<HdAccount> GetAccountsByCoinType(string walletName, CoinType coinType);

        /// <summary>
        /// Builds a transaction to be sent to the network.
        /// </summary>
        /// <param name="walletName">The name of the wallet in which this address is contained.</param>
        /// <param name="coinType">The type of coin for which to get the address.</param>
        /// <param name="accountName">The name of the account in which this address is contained.</param>
        /// <param name="password">The password used to decrypt the private key.</param>
        /// <param name="destinationAddress">The destination address to send the funds to.</param>
        /// <param name="amount">The amount of funds to be sent.</param>
        /// <param name="feeType">The type of fee to be included.</param>
        /// <param name="allowUnconfirmed">Whether or not we allow this transaction to rely on unconfirmed outputs.</param>
        /// <returns></returns>
        (string hex, uint256 transactionId, Money fee) BuildTransaction(string walletName, string accountName, string password, string destinationAddress, Money amount, string feeType, int minConfirmations);

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
        void ProcessBlock(Block block);

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