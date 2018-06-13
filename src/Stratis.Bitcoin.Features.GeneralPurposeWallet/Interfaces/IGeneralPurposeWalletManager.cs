using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.GeneralPurposeWallet;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces
{
	/// <summary>
	/// Interface for a manager providing operations on wallets.
	/// </summary>
	public interface IGeneralPurposeWalletManager
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
		/// Lists all spendable transactions from the account specified in <see cref="GeneralPurposeWalletAccountReference"/>.
		/// </summary>
		/// <returns>A collection of spendable outputs that belong to the given account.</returns>
		IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(GeneralPurposeWalletAccountReference walletAccountReference, int confirmations = 0);

		/// <summary>
		/// Lists all spendable transactions from the given multisig address in the account specified in <see cref="GeneralPurposeWalletAccountReference"/>.
		/// </summary>
		/// <returns>A collection of spendable outputs that belong to the given multisig address in the given account.</returns>
		IEnumerable<UnspentMultiSigOutputReference> GetSpendableMultiSigTransactionsInAccount(GeneralPurposeWalletAccountReference walletAccountReference, Script scriptPubKey, int confirmations = 0);

		/// <summary>
		/// Creates a wallet and persist it as a file on the local system.
		/// </summary>
		/// <param name="password">The password used to encrypt sensitive info.</param>
		/// <param name="name">The name of the wallet.</param>
		/// <param name="multisigMnemonic">Specify the mnemonic for the multisigs instead of allowing a random generation.</param>
		/// <returns>A mnemonic defining the wallet's seed used to generate multisig addresses.</returns>
		Mnemonic CreateWallet(string password, string name, string multisigMnemonic = null);

		/*
		/// <summary>
		/// Loads a wallet from a file.
		/// </summary>
		/// <param name="password">The user's password.</param>
		/// <param name="name">The name of the wallet.</param>
		/// <returns>The wallet.</returns>
		GeneralPurposeWallet LoadWallet(string password, string name);

		/// <summary>
		/// Recovers a wallet.
		/// </summary>
		/// <param name="password">The user's password.</param>
		/// <param name="name">The name of the wallet.</param>
		/// <param name="mnemonic">The user's mnemonic for the wallet.</param>
		/// <param name="passphrase">The passphrase used in the seed.</param>
		/// <param name="creationTime">The date and time this wallet was created.</param>
		/// <returns>The recovered wallet.</returns>
		GeneralPurposeWallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null);
		*/

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
		GeneralPurposeAccount GetUnusedAccount(string walletName, string password);

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
		GeneralPurposeAccount GetUnusedAccount(GeneralPurposeWallet wallet, string password);

		/// <summary>
		/// Gets an address that contains no transaction.
		/// </summary>
		/// <param name="accountReference">The name of the wallet and account</param>
		/// <returns>An unused address or a newly created address, in Base58 format.</returns>
		GeneralPurposeAddress GetUnusedAddress(GeneralPurposeWalletAccountReference accountReference);

		IEnumerable<GeneralPurposeAddress> GetUnusedAddresses(GeneralPurposeWalletAccountReference accountReference, int count);

		/// <summary>
		/// Gets a collection of addresses containing transactions for this coin.
		/// </summary>
		/// <param name="walletName">The wallet name.</param>
		/// <returns>Collection of address history and transaction pairs.</returns>
		IEnumerable<FlatHistory> GetHistory(string walletName);

		/// <summary>
		/// Gets a collection of addresses containing transactions for this coin.
		/// </summary>
		/// <param name="wallet">The wallet to get history from.</param>
		/// <returns></returns>
		IEnumerable<FlatHistory> GetHistory(GeneralPurposeWallet wallet);

		/// <summary>
		/// Gets some general information about a wallet.
		/// </summary>
		/// <param name="walletName">The name of the wallet.</param>
		/// <returns></returns>
		GeneralPurposeWallet GetWallet(string walletName);

		/// <summary>
		/// Gets a list of accounts.
		/// </summary>
		/// <param name="walletName">The name of the wallet to look into.</param>
		/// <returns></returns>
		IEnumerable<GeneralPurposeAccount> GetAccounts(string walletName);

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
		void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true);

		/// <summary>
		/// Saves the wallet into the file system.
		/// </summary>
		/// <param name="wallet">The wallet to save.</param>
		void SaveWallet(GeneralPurposeWallet wallet);

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
		/// <param name="chainedBlock">The height of the last block synced.</param>
		void UpdateLastBlockSyncedHeight(GeneralPurposeWallet wallet, ChainedHeader chainedBlock);

		/// <summary>
		/// Updates all the loaded wallets with the height of the last block synced.
		/// </summary>
		/// <param name="chainedBlock">The height of the last block synced.</param>
		void UpdateLastBlockSyncedHeight(ChainedHeader chainedBlock);

		/// <summary>
		/// Gets a wallet given its name.
		/// </summary>
		/// <param name="walletName">The name of the wallet to get.</param>
		/// <returns>A wallet or null if it doesn't exist</returns>
		GeneralPurposeWallet GetWalletByName(string walletName);

		/// <summary>
		/// Gets the block locator of the first loaded wallet.
		/// </summary>
		/// <returns></returns>
		ICollection<uint256> GetFirstWalletBlockLocator();

		/// <summary>
		/// Gets a change address or create one if all change addresses are used.
		/// </summary>
		/// <param name="account">The account to create the change address.</param>
		/// <returns>The new HD address.</returns>
		GeneralPurposeAddress GetOrCreateChangeAddress(GeneralPurposeAccount account);

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
		/// Gets the lowest LastBlockSyncedHeight of all loaded wallet account roots.
		/// </summary>
		/// <returns>The lowest LastBlockSyncedHeight or null if there are no account roots yet.</returns>
		int? GetEarliestWalletHeight();

		/// <summary>
		/// Gets the oldest wallet creation time.
		/// </summary>
		/// <returns></returns>
		DateTimeOffset GetOldestWalletCreationTime();
	}
}
