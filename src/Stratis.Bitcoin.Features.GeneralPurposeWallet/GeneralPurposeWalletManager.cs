using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Broadcasting;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.GeneralPurposeWallet.Tests")]

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet
{
	/// <summary>
	/// A class that represents a flat view of the wallets history.
	/// </summary>
	public class FlatHistory
	{
		/// <summary>
		/// The address associated with this UTXO
		/// </summary>
		public GeneralPurposeAddress Address { get; set; }

		/// <summary>
		/// The transaction representing the UTXO.
		/// </summary>
		public TransactionData Transaction { get; set; }
	}

	/// <summary>
	/// A manager providing operations on wallets.
	/// </summary>
	public class GeneralPurposeWalletManager : IGeneralPurposeWalletManager
	{
		/// <summary>Size of the buffer of unused addresses maintained in an account. </summary>
		private const int UnusedAddressesBuffer = 20;

		/// <summary>Quantity of accounts created in a wallet file when a wallet is restored.</summary>
		private const int WalletRecoveryAccountsCount = 1;

		/// <summary>Quantity of accounts created in a wallet file when a wallet is created.</summary>
		private const int WalletCreationAccountsCount = 1;

		/// <summary>File extension for wallet files.</summary>
		private const string WalletFileExtension = "nonhdwallet.json";

		/// <summary>Timer for saving wallet files to the file system.</summary>
		private const int WalletSavetimeIntervalInMinutes = 5;

		/// <summary>
		/// A lock object that protects access to the <see cref="Wallet"/>.
		/// Any of the collections inside Wallet must be synchronized using this lock.
		/// </summary>
		private readonly object lockObject;

		/// <summary>The async loop we need to wait upon before we can shut down this manager.</summary>
		private IAsyncLoop asyncLoop;

		/// <summary>Factory for creating background async loop tasks.</summary>
		private readonly IAsyncLoopFactory asyncLoopFactory;

		/// <summary>Gets the list of wallets.</summary>
		public ConcurrentBag<GeneralPurposeWallet> Wallets { get; }

		/// <summary>The type of coin used in this manager.</summary>
		private readonly CoinType coinType;

		/// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
		private readonly Network network;

		/// <summary>The chain of headers.</summary>
		private readonly ConcurrentChain chain;

		/// <summary>Global application life cycle control - triggers when application shuts down.</summary>
		private readonly INodeLifetime nodeLifetime;

		/// <summary>Instance logger.</summary>
		private readonly ILogger logger;

		/// <summary>An object capable of storing <see cref="Wallet"/>s to the file system.</summary>
		private readonly FileStorage<GeneralPurposeWallet> fileStorage;

		/// <summary>The broadcast manager.</summary>
		private readonly IGeneralPurposeWalletBroadcasterManager broadcasterManager;

		/// <summary>Provider of time functions.</summary>
		private readonly IDateTimeProvider dateTimeProvider;

		public uint256 WalletTipHash { get; set; }

		// TODO: a second lookup dictionary is proposed to lookup for spent outputs
		// every time we find a trx that credits we need to add it to this lookup
		// private Dictionary<OutPoint, TransactionData> outpointLookup;
		internal Dictionary<Script, GeneralPurposeAddress> keysLookup;

		internal Dictionary<Script, MultiSigAddress> multiSigKeysLookup;

		public GeneralPurposeWalletManager(
			ILoggerFactory loggerFactory,
			Network network,
			ConcurrentChain chain,
			NodeSettings settings, DataFolder dataFolder,
			IGeneralPurposeWalletFeePolicy walletFeePolicy,
			IAsyncLoopFactory asyncLoopFactory,
			INodeLifetime nodeLifetime,
			IDateTimeProvider dateTimeProvider,
			IGeneralPurposeWalletBroadcasterManager broadcasterManager = null) // no need to know about transactions the node broadcasted
		{
			Guard.NotNull(loggerFactory, nameof(loggerFactory));
			Guard.NotNull(network, nameof(network));
			Guard.NotNull(chain, nameof(chain));
			Guard.NotNull(settings, nameof(settings));
			Guard.NotNull(dataFolder, nameof(dataFolder));
			Guard.NotNull(walletFeePolicy, nameof(walletFeePolicy));
			Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
			Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

			this.lockObject = new object();

			this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
			this.Wallets = new ConcurrentBag<GeneralPurposeWallet>();

			this.network = network;
			this.coinType = (CoinType)network.Consensus.CoinType;
			this.chain = chain;
			this.asyncLoopFactory = asyncLoopFactory;
			this.nodeLifetime = nodeLifetime;
			this.fileStorage = new FileStorage<GeneralPurposeWallet>(dataFolder.WalletPath);
			this.broadcasterManager = broadcasterManager;
			this.dateTimeProvider = dateTimeProvider;

			// register events
			if (this.broadcasterManager != null)
			{
				this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
			}
		}

		private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
		{
			this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
		}

		public void Start()
		{
			this.logger.LogTrace("()");

			// Find wallets and load them in memory.
			IEnumerable<GeneralPurposeWallet> wallets = this.fileStorage.LoadByFileExtension(WalletFileExtension);

			foreach (GeneralPurposeWallet wallet in wallets)
				this.Wallets.Add(wallet);

			// load data in memory for faster lookups
			this.LoadKeysLookupLock();

			// find the last chain block received by the wallet manager.
			this.WalletTipHash = this.LastReceivedBlockHash();

			// save the wallets file every 5 minutes to help against crashes.
			this.asyncLoop = this.asyncLoopFactory.Run("wallet persist job", token =>
			{
				this.logger.LogTrace("()");

				this.SaveWallets();
				this.logger.LogInformation("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

				this.logger.LogTrace("(-)");
				return Task.CompletedTask;
			},
			this.nodeLifetime.ApplicationStopping,
			repeatEvery: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes),
			startAfter: TimeSpan.FromMinutes(WalletSavetimeIntervalInMinutes));

			this.logger.LogTrace("(-)");
		}

		/// <inheritdoc />
		public void Stop()
		{
			this.logger.LogTrace("()");

			if (this.broadcasterManager != null)
				this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

			this.asyncLoop?.Dispose();
			this.SaveWallets();

			this.logger.LogTrace("(-)");
		}

		/// <inheritdoc />
		public GeneralPurposeWallet CreateWallet(string password, string name)
		{
			Guard.NotEmpty(password, nameof(password));
			Guard.NotEmpty(name, nameof(name));
			this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

			// Create a wallet file .
			GeneralPurposeWallet wallet = this.GenerateWalletFile(name);

			// Generate multiple accounts and addresses from the get-go.
			for (int i = 0; i < WalletCreationAccountsCount; i++)
			{
				GeneralPurposeAccount account = wallet.AddNewAccount($"account {i}", this.coinType, this.dateTimeProvider.GetTimeOffset());
				account.CreateAddresses(this.network, UnusedAddressesBuffer);
				account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
			}

			// If the chain is downloaded, we set the height of the newly created wallet to it.
			// However, if the chain is still downloading when the user creates a wallet,
			// we wait until it is downloaded in order to set it. Otherwise, the height of the wallet will be the height of the chain at that moment.
			if (this.chain.IsDownloaded())
			{
				this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
			}
			else
			{
				this.UpdateWhenChainDownloaded(new[] { wallet }, DateTime.Now);
			}

			// Save the changes to the file and add addresses to be tracked.
			this.SaveWallet(wallet);
			this.Load(wallet);
			this.LoadKeysLookupLock();

			this.logger.LogTrace("(-)");
			return wallet;
		}

		// TODO: Port this logic in a form that makes sense for a non-HD wallet
		/*
		/// <inheritdoc />
		public GeneralPurposeWallet LoadWallet(string password, string name)
		{
			Guard.NotEmpty(password, nameof(password));
			Guard.NotEmpty(name, nameof(name));
			this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

			// Load the file from the local system.
			GeneralPurposeWallet wallet = this.fileStorage.LoadByFileName($"{name}.{WalletFileExtension}");

			// Check the password.
			try
			{
				Key.Parse(wallet.EncryptedSeed, password, wallet.Network);
			}
			catch (Exception ex)
			{
				this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
				this.logger.LogTrace("(-)[EXCEPTION]");
				throw new SecurityException(ex.Message);
			}

			this.Load(wallet);

			this.logger.LogTrace("(-)");
			return wallet;
		}

		/// <inheritdoc />
		public GeneralPurposeWallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null)
		{
			Guard.NotEmpty(password, nameof(password));
			Guard.NotEmpty(name, nameof(name));
			Guard.NotEmpty(mnemonic, nameof(mnemonic));
			this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

			// For now the passphrase is set to be the password by default.
			if (passphrase == null)
				passphrase = password;

			// Generate the root seed used to generate keys.
			ExtKey extendedKey;
			try
			{
				extendedKey = HdOperations.GetHdPrivateKey(mnemonic, passphrase);
			}
			catch (NotSupportedException ex)
			{
				this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
				this.logger.LogTrace("(-)[EXCEPTION]");

				if (ex.Message == "Unknown")
					throw new GeneralPurposeWalletException("Please make sure you enter valid mnemonic words.");

				throw;
			}

			// Create a wallet file.
			string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
			GeneralPurposeWallet wallet = this.GenerateWalletFile(name, encryptedSeed, extendedKey.ChainCode, creationTime);

			// Generate multiple accounts and addresses from the get-go.
			for (int i = 0; i < WalletRecoveryAccountsCount; i++)
			{
				GeneralPurposeAccount account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
				account.CreateAddresses(this.network, UnusedAddressesBuffer);
				account.CreateAddresses(this.network, UnusedAddressesBuffer, true);
			}

			// If the chain is downloaded, we set the height of the recovered wallet to that of the recovery date.
			// However, if the chain is still downloading when the user restores a wallet,
			// we wait until it is downloaded in order to set it. Otherwise, the height of the wallet may not be known.
			if (this.chain.IsDownloaded())
			{
				int blockSyncStart = this.chain.GetHeightAtTime(creationTime);
				this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(blockSyncStart));
			}
			else
			{
				this.UpdateWhenChainDownloaded(new[] { wallet }, creationTime);
			}

			// Save the changes to the file and add addresses to be tracked.
			this.SaveWallet(wallet);
			this.Load(wallet);
			this.LoadKeysLookupLock();

			this.logger.LogTrace("(-)");
			return wallet;
		}
		*/

		/// <inheritdoc />
		public GeneralPurposeAccount GetUnusedAccount(string walletName, string password)
		{
			Guard.NotEmpty(walletName, nameof(walletName));
			Guard.NotEmpty(password, nameof(password));
			this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

			GeneralPurposeWallet wallet = this.GetWalletByName(walletName);

			GeneralPurposeAccount res = this.GetUnusedAccount(wallet, password);
			this.logger.LogTrace("(-)");
			return res;
		}

		/// <inheritdoc />
		public GeneralPurposeAccount GetUnusedAccount(GeneralPurposeWallet wallet, string password)
		{
			Guard.NotNull(wallet, nameof(wallet));
			Guard.NotEmpty(password, nameof(password));
			this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

			GeneralPurposeAccount account;

			lock (this.lockObject)
			{
				account = wallet.GetFirstUnusedAccount(this.coinType);

				if (account != null)
				{
					return account;
				}

				// No unused account was found, create a new one.
				account = wallet.AddNewAccount(password, this.coinType, this.dateTimeProvider.GetTimeOffset());
			}

			// save the changes to the file
			this.SaveWallet(wallet);

			this.logger.LogTrace("(-)");
			return account;
		}

		/// <inheritdoc />
		public GeneralPurposeAddress GetUnusedAddress(GeneralPurposeWalletAccountReference accountReference)
		{
			this.logger.LogTrace("({0}:'{1}')", nameof(accountReference), accountReference);

			GeneralPurposeAddress res = this.GetUnusedAddresses(accountReference, 1).Single();

			this.logger.LogTrace("(-)");
			return res;
		}

		/// <inheritdoc />
		public IEnumerable<GeneralPurposeAddress> GetUnusedAddresses(GeneralPurposeWalletAccountReference accountReference, int count)
		{
			Guard.NotNull(accountReference, nameof(accountReference));
			Guard.Assert(count > 0);
			this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(accountReference), accountReference, nameof(count), count);

			GeneralPurposeWallet wallet = this.GetWalletByName(accountReference.WalletName);

			bool generated = false;
			IEnumerable<GeneralPurposeAddress> addresses;

			lock (this.lockObject)
			{
				// Get the account.
				GeneralPurposeAccount account = wallet.GetAccountByCoinType(accountReference.AccountName, this.coinType);

				List<GeneralPurposeAddress> unusedAddresses = account.ExternalAddresses.Where(acc => !acc.Transactions.Any()).ToList();
				int diff = unusedAddresses.Count - count;
				if (diff < 0)
				{
					account.CreateAddresses(this.network, Math.Abs(diff), isChange: false);
					generated = true;
				}

				addresses = account
					.ExternalAddresses
					.Where(acc => !acc.Transactions.Any())
					.Take(count);
			}

			if (generated)
			{
				// save the changes to the file
				this.SaveWallet(wallet);

				// adds the address to the list of tracked addresses
				this.LoadKeysLookupLock();
			}

			this.logger.LogTrace("(-)");
			return addresses;
		}

		/// <inheritdoc />
		public GeneralPurposeAddress GetOrCreateChangeAddress(GeneralPurposeAccount account)
		{
			this.logger.LogTrace("()");
			GeneralPurposeAddress changeAddress = null;

			lock (this.lockObject)
			{
				// get address to send the change to
				changeAddress = account.GetFirstUnusedChangeAddress();

				// no more change addresses left. create a new one.
				if (changeAddress == null)
				{
					var accountAddress = account.CreateAddresses(this.network, 1, isChange: true).Single();
					changeAddress = account.InternalAddresses.First(a => a.Address == accountAddress);
				}
			}

			// Adds the address to the list of tracked addresses.
			this.LoadKeysLookupLock();

			// Persist the address to the wallet files.
			this.SaveWallets();

			this.logger.LogTrace("(-)");
			return changeAddress;
		}

		/// <inheritdoc />
		public (string folderPath, IEnumerable<string>) GetWalletsFiles()
		{
			return (this.fileStorage.FolderPath, this.fileStorage.GetFilesNames(this.GetWalletFileExtension()));
		}

		/// <inheritdoc />
		public IEnumerable<FlatHistory> GetHistory(string walletName)
		{
			Guard.NotEmpty(walletName, nameof(walletName));
			this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

			// In order to calculate the fee properly we need to retrieve all the transactions with spending details.
			GeneralPurposeWallet wallet = this.GetWalletByName(walletName);
			IEnumerable<FlatHistory> res = this.GetHistory(wallet);

			this.logger.LogTrace("(-):*.Count={0}", res.Count());
			return res;
		}

		/// <inheritdoc />
		public IEnumerable<FlatHistory> GetHistory(GeneralPurposeWallet wallet)
		{
			Guard.NotNull(wallet, nameof(wallet));
			FlatHistory[] items = null;
			lock (this.lockObject)
			{
				// Get transactions contained in the wallet.
				items = this.GetHistoryInternal(wallet).SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s, Transaction = t })).ToArray();
			}

			this.logger.LogTrace("(-):*.Count={0}", items.Count());
			return items;
		}

		/// <summary>
		/// Gets a collection of addresses that have transactions associated with them.
		/// </summary>
		/// <param name="wallet">The wallet to get the history from.</param>
		/// <returns>A collection of addresses that have transactions associated with them.</returns>
		private IEnumerable<GeneralPurposeAddress> GetHistoryInternal(GeneralPurposeWallet wallet)
		{
			IEnumerable<GeneralPurposeAccount> accounts = wallet.GetAccountsByCoinType(this.coinType);

			foreach (GeneralPurposeAddress address in accounts.SelectMany(a => a.ExternalAddresses).Concat(accounts.SelectMany(a => a.InternalAddresses)))
			{
				if (address.Transactions.Any())
				{
					yield return address;
				}
			}
		}

		/// <inheritdoc />
		public GeneralPurposeWallet GetWallet(string walletName)
		{
			Guard.NotEmpty(walletName, nameof(walletName));
			this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

			GeneralPurposeWallet wallet = this.GetWalletByName(walletName);

			this.logger.LogTrace("(-)");
			return wallet;
		}

		/// <inheritdoc />
		public IEnumerable<GeneralPurposeAccount> GetAccounts(string walletName)
		{
			Guard.NotEmpty(walletName, nameof(walletName));
			this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

			GeneralPurposeWallet wallet = this.GetWalletByName(walletName);

			GeneralPurposeAccount[] res = null;
			lock (this.lockObject)
			{
				res = wallet.GetAccountsByCoinType(this.coinType).ToArray();
			}

			this.logger.LogTrace("(-):*.Count={0}", res.Count());
			return res;
		}

		/// <inheritdoc />
		public int LastBlockHeight()
		{
			this.logger.LogTrace("()");

			if (!this.Wallets.Any())
			{
				int height = this.chain.Tip.Height;
				this.logger.LogTrace("(-)[NO_WALLET]:{0}", height);
				return height;
			}

			int res;
			lock (this.lockObject)
			{
				res = this.Wallets.Min(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType)?.LastBlockSyncedHeight) ?? 0;
			}
			this.logger.LogTrace("(-):{0}", res);
			return res;
		}

		/// <inheritdoc />
		public bool ContainsWallets => this.Wallets.Any();

		/// <summary>
		/// Gets the hash of the last block received by the wallets.
		/// </summary>
		/// <returns>Hash of the last block received by the wallets.</returns>
		public uint256 LastReceivedBlockHash()
		{
			this.logger.LogTrace("()");

			if (!this.Wallets.Any())
			{
				uint256 hash = this.chain.Tip.HashBlock;
				this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", hash);
				return hash;
			}

			uint256 lastBlockSyncedHash;
			lock (this.lockObject)
			{
				lastBlockSyncedHash = this.Wallets
					.Select(w => w.AccountsRoot.SingleOrDefault(a => a.CoinType == this.coinType))
					.Where(w => w != null)
					.OrderBy(o => o.LastBlockSyncedHeight)
					.FirstOrDefault()?.LastBlockSyncedHash;

				// If details about the last block synced are not present in the wallet,
				// find out which is the oldest wallet and set the last block synced to be the one at this date.
				if (lastBlockSyncedHash == null)
				{
					this.logger.LogWarning("There were no details about the last block synced in the wallets.");
					DateTimeOffset earliestWalletDate = this.Wallets.Min(c => c.CreationTime);
					this.UpdateWhenChainDownloaded(this.Wallets, earliestWalletDate.DateTime);
					lastBlockSyncedHash = this.chain.Tip.HashBlock;
				}
			}

			this.logger.LogTrace("(-):'{0}'", lastBlockSyncedHash);
			return lastBlockSyncedHash;
		}

		/// <inheritdoc />
		public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
		{
			Guard.NotEmpty(walletName, nameof(walletName));
			this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletName), walletName, nameof(confirmations), confirmations);

			GeneralPurposeWallet wallet = this.GetWalletByName(walletName);
			UnspentOutputReference[] res = null;
			lock (this.lockObject)
			{
				res = wallet.GetAllSpendableTransactions(this.coinType, this.chain.Tip.Height, confirmations).ToArray();
			}

			this.logger.LogTrace("(-):*.Count={0}", res.Count());
			return res;
		}

		/// <inheritdoc />
		public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(GeneralPurposeWalletAccountReference walletAccountReference, int confirmations = 0)
		{
			Guard.NotNull(walletAccountReference, nameof(walletAccountReference));
			this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletAccountReference), walletAccountReference, nameof(confirmations), confirmations);

			GeneralPurposeWallet wallet = this.GetWalletByName(walletAccountReference.WalletName);
			UnspentOutputReference[] res = null;
			lock (this.lockObject)
			{
				GeneralPurposeAccount account = wallet.GetAccountByCoinType(walletAccountReference.AccountName, this.coinType);

				if (account == null)
				{
					this.logger.LogTrace("(-)[ACT_NOT_FOUND]");
					throw new GeneralPurposeWalletException(
						$"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
				}

				res = account.GetSpendableTransactions(this.chain.Tip.Height, confirmations).ToArray();
			}

			this.logger.LogTrace("(-):*.Count={0}", res.Count());
			return res;
		}

		/// <inheritdoc />
		public IEnumerable<UnspentMultiSigOutputReference> GetSpendableMultiSigTransactionsInAccount(GeneralPurposeWalletAccountReference walletAccountReference, Script scriptPubKey, int confirmations = 0)
		{
			Guard.NotNull(walletAccountReference, nameof(walletAccountReference));
			this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletAccountReference), walletAccountReference, nameof(confirmations), confirmations);

			GeneralPurposeWallet wallet = this.GetWalletByName(walletAccountReference.WalletName);
			UnspentMultiSigOutputReference[] res = null;
			lock (this.lockObject)
			{
				GeneralPurposeAccount account = wallet.GetAccountByCoinType(walletAccountReference.AccountName, this.coinType);

				if (account == null)
				{
					this.logger.LogTrace("(-)[ACT_NOT_FOUND]");
					throw new GeneralPurposeWalletException(
						$"Account '{walletAccountReference.AccountName}' in wallet '{walletAccountReference.WalletName}' not found.");
				}

				res = account.GetSpendableMultiSigTransactions(scriptPubKey, this.chain.Tip.Height, confirmations).ToArray();
			}

			this.logger.LogTrace("(-):*.Count={0}", res.Count());
			return res;
		}

		/// <inheritdoc />
		public void RemoveBlocks(ChainedHeader fork)
		{
			Guard.NotNull(fork, nameof(fork));
			this.logger.LogTrace("({0}:'{1}'", nameof(fork), fork);

			if (this.keysLookup == null)
				this.LoadKeysLookupLock();

			lock (this.lockObject)
			{
				IEnumerable<GeneralPurposeAddress> allAddresses = this.keysLookup.Values;
				foreach (GeneralPurposeAddress address in allAddresses)
				{
					// Remove all the UTXO that have been reorged.
					IEnumerable<TransactionData> makeUnspendable = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
					foreach (TransactionData transactionData in makeUnspendable)
						address.Transactions.Remove(transactionData);

					// Bring back all the UTXO that are now spendable after the reorg.
					IEnumerable<TransactionData> makeSpendable = address.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
					foreach (TransactionData transactionData in makeSpendable)
						transactionData.SpendingDetails = null;
				}

				this.UpdateLastBlockSyncedHeight(fork);
			}

			this.logger.LogTrace("(-)");
		}

		/// <inheritdoc />
		public void ProcessBlock(Block block, ChainedHeader chainedBlock)
		{
			Guard.NotNull(block, nameof(block));
			Guard.NotNull(chainedBlock, nameof(chainedBlock));
			this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(block), block.GetHash(), nameof(chainedBlock), chainedBlock);

			// If there is no wallet yet, update the wallet tip hash and do nothing else.
			if (!this.Wallets.Any())
			{
				this.WalletTipHash = chainedBlock.HashBlock;
				this.logger.LogTrace("(-)[NO_WALLET]");
				return;
			}

			// Is this the next block.
			if (chainedBlock.Header.HashPrevBlock != this.WalletTipHash)
			{
				this.logger.LogTrace("New block's previous hash '{0}' does not match current wallet's tip hash '{1}'.", chainedBlock.Header.HashPrevBlock, this.WalletTipHash);

				// Are we still on the main chain.
				ChainedHeader current = this.chain.GetBlock(this.WalletTipHash);
				if (current == null)
				{
					this.logger.LogTrace("(-)[REORG]");
					throw new GeneralPurposeWalletException("Reorg");
				}

				// The block coming in to the wallet should
				// never be ahead of the wallet, if the block is behind let it pass.
				if (chainedBlock.Height > current.Height)
				{
					this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
					throw new GeneralPurposeWalletException("block too far in the future has arrived to the wallet");
				}
			}

			lock (this.lockObject)
			{
				foreach (Transaction transaction in block.Transactions)
					this.ProcessTransaction(transaction, chainedBlock.Height, block, true);

				// Update the wallets with the last processed block height.
				this.UpdateLastBlockSyncedHeight(chainedBlock);
			}

			this.logger.LogTrace("(-)");
		}

		/// <inheritdoc />
		public void ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
		{
			Guard.NotNull(transaction, nameof(transaction));
			uint256 hash = transaction.GetHash();
			this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(transaction), hash, nameof(blockHeight), blockHeight);

			// Load the keys for lookup if they are not loaded yet.
			if (this.keysLookup == null)
				this.LoadKeysLookupLock();

			// Load the multisig keys for lookup if they are not loaded yet.
			if (this.multiSigKeysLookup == null)
				this.LoadMultiSigKeysLookupLock();

			var foundTrx = new List<Tuple<Script, uint256>>();

			lock (this.lockObject)
			{
				// Check the outputs.
				foreach (TxOut utxo in transaction.Outputs)
				{
					// Check if the outputs contain one of our addresses.
					if (this.keysLookup.TryGetValue(utxo.ScriptPubKey, out GeneralPurposeAddress pubKey))
					{
						this.AddTransactionToWallet(transaction.ToHex(), hash, transaction.Time, transaction.IsCoinStake, transaction.Outputs.IndexOf(utxo),
							utxo.Value, utxo.ScriptPubKey, blockHeight, block, isPropagated, false);
						foundTrx.Add(Tuple.Create(utxo.ScriptPubKey, hash));
					}

					// Check if the outputs contain one of our multisig addresses.
					if (this.multiSigKeysLookup.TryGetValue(utxo.ScriptPubKey, out MultiSigAddress multiSigKey))
					{
						this.AddTransactionToWallet(transaction.ToHex(), hash, transaction.Time, transaction.IsCoinStake, transaction.Outputs.IndexOf(utxo),
							utxo.Value, utxo.ScriptPubKey, blockHeight, block, isPropagated, true);
						foundTrx.Add(Tuple.Create(utxo.ScriptPubKey, hash));
					}
				}

				// Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
				foreach (TxIn input in transaction.Inputs.Where(txIn => this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions).Any(trackedTx => trackedTx.Id == txIn.PrevOut.Hash && trackedTx.Index == txIn.PrevOut.N)))
				{
					TransactionData tTx = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions).Single(trackedTx => trackedTx.Id == input.PrevOut.Hash && trackedTx.Index == input.PrevOut.N);

					// Find the script this input references.
					Script keyToSpend = this.keysLookup.First(v => v.Value.Transactions.Contains(tTx)).Key;

					// Get the details of the outputs paid out.
					IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
					{
						// If script is empty ignore it.
						if (o.IsEmpty)
							return false;

						// Check if the destination script is one of the wallet's.
						bool found = this.keysLookup.TryGetValue(o.ScriptPubKey, out GeneralPurposeAddress addr);

						// Include the keys not included in our wallets (external payees).
						if (!found)
							return true;

						// Include the keys that are in the wallet but that are for receiving
						// addresses (which would mean the user paid itself). 
						// We also exclude the keys involved in a staking transaction.
						return !addr.IsChangeAddress && !transaction.IsCoinStake;
					});

					this.AddSpendingTransactionToWallet(transaction.ToHex(), hash, transaction.Time, transaction.IsCoinStake, paidOutTo, tTx.Id, tTx.Index, blockHeight, block, false);
				}

				// Check the inputs - include those that have a reference to a transaction containing one of our multisig scripts and the same index.
				foreach (TxIn input in transaction.Inputs.Where(txIn => this.multiSigKeysLookup.Values.Distinct().SelectMany(v => v.Transactions).Any(trackedTx => trackedTx.Id == txIn.PrevOut.Hash && trackedTx.Index == txIn.PrevOut.N)))
				{
					TransactionData tTx = this.multiSigKeysLookup.Values.Distinct().SelectMany(v => v.Transactions).Single(trackedTx => trackedTx.Id == input.PrevOut.Hash && trackedTx.Index == input.PrevOut.N);

					// Find the script this input references.
					Script keyToSpend = this.multiSigKeysLookup.First(v => v.Value.Transactions.Contains(tTx)).Key;

					// Get the details of the outputs paid out.
					IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
					{
						// If script is empty ignore it.
						if (o.IsEmpty)
							return false;

						// Check if the destination script is one of the wallet's.
						bool found = this.multiSigKeysLookup.TryGetValue(o.ScriptPubKey, out MultiSigAddress addr);

						// Include the keys not included in our wallets (external payees).
						if (!found)
							return true;
						
						// We exclude the keys involved in a staking transaction.
						return !transaction.IsCoinStake;
					});

					this.AddSpendingTransactionToWallet(transaction.ToHex(), hash, transaction.Time, transaction.IsCoinStake, paidOutTo, tTx.Id, tTx.Index, blockHeight, block, true);
				}
			}

			if (foundTrx.Any())
			{
				this.LoadKeysLookupLock();
				this.LoadMultiSigKeysLookupLock();
			}

			this.logger.LogTrace("(-)");
		}

		/// <summary>
		/// Adds a transaction that credits the wallet with new coins.
		/// This method is can be called many times for the same transaction (idempotent).
		/// </summary>
		/// <param name="transactionHash">The transaction hash.</param>
		/// <param name="time">The time.</param>
		/// <param name="isCoinStake">A value indicating whether this is a coin stake transaction or not.</param>
		/// <param name="index">The index.</param>
		/// <param name="amount">The amount.</param>
		/// <param name="script">The script.</param>
		/// <param name="blockHeight">Height of the block.</param>
		/// <param name="block">The block containing the transaction to add.</param>
		/// <param name="transactionHex">The hexadecimal representation of the transaction.</param>
		/// <param name="isPropagated">Propagation state of the transaction.</param>
		/// <param name="isMultiSig">Whether or not this transaction affects one of the wallet's multisig addresses.</param>
		private void AddTransactionToWallet(string transactionHex, uint256 transactionHash, uint time, bool isCoinStake, int index, Money amount, Script script,
			int? blockHeight = null, Block block = null, bool isPropagated = true, bool isMultiSig = false)
		{
			this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:{5},{6}:{7},{8}:{9},{10}:{11},{12}:{13})", nameof(transactionHex), transactionHex,
				nameof(transactionHash), transactionHash, nameof(time), time, nameof(isCoinStake), isCoinStake, nameof(index), index, nameof(amount), amount, nameof(blockHeight), blockHeight);

			if (!isMultiSig)
			{
				// Get the collection of transactions to add to.
				this.keysLookup.TryGetValue(script, out GeneralPurposeAddress address);
				ICollection<TransactionData> addressTransactions = address.Transactions;

				// Check if a similar UTXO exists or not (same transaction ID and same index).
				// New UTXOs are added, existing ones are updated.
				TransactionData foundTransaction =
					addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
				if (foundTransaction == null)
				{
					this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
					var newTransaction = new TransactionData
					{
						Amount = amount,
						IsCoinStake = isCoinStake == false ? (bool?) null : true,
						BlockHeight = blockHeight,
						BlockHash = block?.GetHash(),
						Id = transactionHash,
						CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
						Index = index,
						ScriptPubKey = script,
						Hex = transactionHex,
						IsPropagated = isPropagated
					};

					// add the Merkle proof to the (non-spending) transaction
					if (block != null)
					{
						newTransaction.MerkleProof = new MerkleBlock(block, new[] {transactionHash}).PartialMerkleTree;
					}

					addressTransactions.Add(newTransaction);
				}
				else
				{
					this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

					// Update the block height and block hash.
					if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
					{
						foundTransaction.BlockHeight = blockHeight;
						foundTransaction.BlockHash = block?.GetHash();
					}

					// Update the block time.
					if (block != null)
					{
						foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
					}

					// Add the Merkle proof now that the transaction is confirmed in a block.
					if ((block != null) && (foundTransaction.MerkleProof == null))
					{
						foundTransaction.MerkleProof = new MerkleBlock(block, new[] {transactionHash}).PartialMerkleTree;
					}

					if (isPropagated)
						foundTransaction.IsPropagated = true;
				}

				this.TransactionFoundInternal(script);
				this.logger.LogTrace("(-)");
			}
			else
			{
				// Get the collection of transactions to add to.
				this.multiSigKeysLookup.TryGetValue(script, out MultiSigAddress address);
				ICollection<TransactionData> addressTransactions = address.Transactions;

				// Check if a similar UTXO exists or not (same transaction ID and same index).
				// New UTXOs are added, existing ones are updated.
				TransactionData foundTransaction =
					addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
				if (foundTransaction == null)
				{
					this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
					var newTransaction = new TransactionData
					{
						Amount = amount,
						IsCoinStake = isCoinStake == false ? (bool?)null : true,
						BlockHeight = blockHeight,
						BlockHash = block?.GetHash(),
						Id = transactionHash,
						CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
						Index = index,
						ScriptPubKey = script,
						Hex = transactionHex,
						IsPropagated = isPropagated
					};

					// add the Merkle proof to the (non-spending) transaction
					if (block != null)
					{
						newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
					}

					addressTransactions.Add(newTransaction);
				}
				else
				{
					this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

					// Update the block height and block hash.
					if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
					{
						foundTransaction.BlockHeight = blockHeight;
						foundTransaction.BlockHash = block?.GetHash();
					}

					// Update the block time.
					if (block != null)
					{
						foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
					}

					// Add the Merkle proof now that the transaction is confirmed in a block.
					if ((block != null) && (foundTransaction.MerkleProof == null))
					{
						foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
					}

					if (isPropagated)
						foundTransaction.IsPropagated = true;
				}

				this.TransactionFoundInternal(script);
				this.logger.LogTrace("(-)");
			}
		}

		/// <summary>
		/// Mark an output as spent, the credit of the output will not be used to calculate the balance.
		/// The output will remain in the wallet for history (and reorg).
		/// </summary>
		/// <param name="transactionHash">The transaction hash.</param>
		/// <param name="time">The time.</param>
		/// <param name="isCoinStake">A value indicating whether this is a coin stake transaction or not.</param>
		/// <param name="paidToOutputs">A list of payments made out</param>
		/// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
		/// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
		/// <param name="blockHeight">Height of the block.</param>
		/// <param name="block">The block containing the transaction to add.</param>
		/// <param name="transactionHex">The hexadecimal representation of the transaction.</param>
		/// <param name="isMultiSig">Whether or not this transaction affects one of the wallet's multisig addresses.</param>
		private void AddSpendingTransactionToWallet(string transactionHex, uint256 transactionHash, uint time, bool isCoinStake, IEnumerable<TxOut> paidToOutputs,
			uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null, bool isMultiSig = false)
		{
			this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:{5},{6}:'{7}',{8}:{9},{10}:{11},{12}:{13})", nameof(transactionHex), transactionHex,
				nameof(transactionHash), transactionHash, nameof(time), time, nameof(isCoinStake), isCoinStake, nameof(spendingTransactionId), spendingTransactionId, nameof(spendingTransactionIndex), spendingTransactionIndex, nameof(blockHeight), blockHeight);

			if (!isMultiSig)
			{
				// Get the transaction being spent.
				TransactionData spentTransaction = this.keysLookup.Values.Distinct().SelectMany(v => v.Transactions)
					.SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
				if (spentTransaction == null)
				{
					// Strange, why would it be null?
					this.logger.LogTrace("(-)[TX_NULL]");
					return;
				}

				// If the details of this spending transaction are seen for the first time.
				if (spentTransaction.SpendingDetails == null)
				{
					this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

					List<PaymentDetails> payments = new List<PaymentDetails>();
					foreach (TxOut paidToOutput in paidToOutputs)
					{
						// Figure out how to retrieve the destination address.
						string destinationAddress = string.Empty;
						ScriptTemplate scriptTemplate = paidToOutput.ScriptPubKey.FindTemplate(this.network);
						switch (scriptTemplate.Type)
						{
							// Pay to PubKey can be found in outputs of staking transactions.
							case TxOutType.TX_PUBKEY:
								PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(paidToOutput.ScriptPubKey);
								destinationAddress = pubKey.GetAddress(this.network).ToString();
								break;
							// Pay to PubKey hash is the regular, most common type of output.
							case TxOutType.TX_PUBKEYHASH:
								destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
								break;
							case TxOutType.TX_NONSTANDARD:
								break;
							case TxOutType.TX_SCRIPTHASH:
								destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
								break;
							case TxOutType.TX_MULTISIG:
							case TxOutType.TX_NULL_DATA:
							case TxOutType.TX_SEGWIT:
								break;
						}

						payments.Add(new PaymentDetails
						{
							DestinationScriptPubKey = paidToOutput.ScriptPubKey,
							DestinationAddress = destinationAddress,
							Amount = paidToOutput.Value
						});
					}

					SpendingDetails spendingDetails = new SpendingDetails
					{
						TransactionId = transactionHash,
						Payments = payments,
						CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
						BlockHeight = blockHeight,
						Hex = transactionHex,
						IsCoinStake = isCoinStake == false ? (bool?) null : true
					};

					spentTransaction.SpendingDetails = spendingDetails;
					spentTransaction.MerkleProof = null;
				}
				else // If this spending transaction is being confirmed in a block.
				{
					this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

					// Update the block height.
					if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
					{
						spentTransaction.SpendingDetails.BlockHeight = blockHeight;
					}

					// Update the block time to be that of the block in which the transaction is confirmed.
					if (block != null)
					{
						spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
					}
				}
			}
			else
			{
				// Get the transaction being spent.
				TransactionData spentTransaction = this.multiSigKeysLookup.Values.Distinct().SelectMany(v => v.Transactions)
					.SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
				if (spentTransaction == null)
				{
					// Strange, why would it be null?
					this.logger.LogTrace("(-)[TX_NULL]");
					return;
				}

				// If the details of this spending transaction are seen for the first time.
				if (spentTransaction.SpendingDetails == null)
				{
					this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

					List<PaymentDetails> payments = new List<PaymentDetails>();
					foreach (TxOut paidToOutput in paidToOutputs)
					{
						// Figure out how to retrieve the destination address.
						string destinationAddress = string.Empty;
						ScriptTemplate scriptTemplate = paidToOutput.ScriptPubKey.FindTemplate(this.network);
						switch (scriptTemplate.Type)
						{
							// Pay to PubKey can be found in outputs of staking transactions.
							case TxOutType.TX_PUBKEY:
								PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(paidToOutput.ScriptPubKey);
								destinationAddress = pubKey.GetAddress(this.network).ToString();
								break;
							// Pay to PubKey hash is the regular, most common type of output.
							case TxOutType.TX_PUBKEYHASH:
								destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
								break;
							case TxOutType.TX_NONSTANDARD:
								break;
							case TxOutType.TX_SCRIPTHASH:
								destinationAddress = paidToOutput.ScriptPubKey.GetDestinationAddress(this.network).ToString();
								break;
							case TxOutType.TX_MULTISIG:
							case TxOutType.TX_NULL_DATA:
							case TxOutType.TX_SEGWIT:
								break;
						}

						payments.Add(new PaymentDetails
						{
							DestinationScriptPubKey = paidToOutput.ScriptPubKey,
							DestinationAddress = destinationAddress,
							Amount = paidToOutput.Value
						});
					}

					SpendingDetails spendingDetails = new SpendingDetails
					{
						TransactionId = transactionHash,
						Payments = payments,
						CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? time),
						BlockHeight = blockHeight,
						Hex = transactionHex,
						IsCoinStake = isCoinStake == false ? (bool?)null : true
					};

					spentTransaction.SpendingDetails = spendingDetails;
					spentTransaction.MerkleProof = null;
				}
				else // If this spending transaction is being confirmed in a block.
				{
					this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

					// Update the block height.
					if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
					{
						spentTransaction.SpendingDetails.BlockHeight = blockHeight;
					}

					// Update the block time to be that of the block in which the transaction is confirmed.
					if (block != null)
					{
						spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
					}
				}
			}

			this.logger.LogTrace("(-)");
		}

		public void TransactionFoundInternal(Script script)
		{
			this.logger.LogTrace("()");

			foreach (GeneralPurposeWallet wallet in this.Wallets)
			{
				foreach (GeneralPurposeAccount account in wallet.GetAccountsByCoinType(this.coinType))
				{
					bool isChange;
					if (account.ExternalAddresses.Any(address => address.ScriptPubKey == script))
					{
						isChange = false;
					}
					else if (account.InternalAddresses.Any(address => address.ScriptPubKey == script))
					{
						isChange = true;
					}
					else
					{
						continue;
					}

					// Calculate how many accounts to add to keep a buffer of 20 unused addresses.
					int addressesCount = isChange ? account.InternalAddresses.Count() : account.ExternalAddresses.Count();
					int emptyAddressesCount = addressesCount - 1;
					int accountsToAdd = UnusedAddressesBuffer - emptyAddressesCount;
					account.CreateAddresses(this.network, accountsToAdd, isChange);

					this.LoadKeysLookupLock();

					// Persists the address to the wallet file.
					this.SaveWallet(wallet);
				}
			}

			this.logger.LogTrace("()");
		}

		/// <inheritdoc />
		public void DeleteWallet()
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc />
		public void SaveWallets()
		{
			foreach (GeneralPurposeWallet wallet in this.Wallets)
			{
				this.SaveWallet(wallet);
			}
		}

		/// <inheritdoc />
		public void SaveWallet(GeneralPurposeWallet wallet)
		{
			Guard.NotNull(wallet, nameof(wallet));
			this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

			lock (this.lockObject)
			{
				this.fileStorage.SaveToFile(wallet, $"{wallet.Name}.{WalletFileExtension}");
			}

			this.logger.LogTrace("(-)");
		}

		/// <inheritdoc />
		public string GetWalletFileExtension()
		{
			return WalletFileExtension;
		}

		/// <inheritdoc />
		public void UpdateLastBlockSyncedHeight(ChainedHeader chainedBlock)
		{
			Guard.NotNull(chainedBlock, nameof(chainedBlock));
			this.logger.LogTrace("({0}:'{1}')", nameof(chainedBlock), chainedBlock);

			// Update the wallets with the last processed block height.
			foreach (GeneralPurposeWallet wallet in this.Wallets)
			{
				this.UpdateLastBlockSyncedHeight(wallet, chainedBlock);
			}

			this.WalletTipHash = chainedBlock.HashBlock;
			this.logger.LogTrace("(-)");
		}

		/// <inheritdoc />
		public void UpdateLastBlockSyncedHeight(GeneralPurposeWallet wallet, ChainedHeader chainedBlock)
		{
			Guard.NotNull(wallet, nameof(wallet));
			Guard.NotNull(chainedBlock, nameof(chainedBlock));
			this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(wallet), wallet.Name, nameof(chainedBlock), chainedBlock);

			// the block locator will help when the wallet
			// needs to rewind this will be used to find the fork
			wallet.BlockLocator = chainedBlock.GetLocator().Blocks;

			lock (this.lockObject)
			{
				// update the wallets with the last processed block height
				foreach (AccountRoot accountRoot in wallet.AccountsRoot.Where(a => a.CoinType == this.coinType))
				{
					accountRoot.LastBlockSyncedHeight = chainedBlock.Height;
					accountRoot.LastBlockSyncedHash = chainedBlock.HashBlock;
				}
			}

			this.logger.LogTrace("(-)");
		}

		/// <summary>
		/// Generates the wallet file.
		/// </summary>
		/// <param name="name">The name of the wallet.</param>
		/// <param name="encryptedSeed">The seed for this wallet, password encrypted.</param>
		/// <param name="chainCode">The chain code.</param>
		/// <param name="creationTime">The time this wallet was created.</param>
		/// <returns>The wallet object that was saved into the file system.</returns>
		/// <exception cref="WalletException">Thrown if wallet cannot be created.</exception>
		private GeneralPurposeWallet GenerateWalletFile(string name, DateTimeOffset? creationTime = null)
		{
			Guard.NotEmpty(name, nameof(name));
			this.logger.LogTrace("({0}:'{1}')", nameof(name), name);

			// Check if any wallet file already exists, with case insensitive comparison.
			if (this.Wallets.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				this.logger.LogTrace("(-)[WALLET_ALREADY_EXISTS]");
				throw new GeneralPurposeWalletException($"Wallet with name '{name}' already exists.");
			}

			GeneralPurposeWallet walletFile = new GeneralPurposeWallet
			{
				Name = name,
				CreationTime = creationTime ?? this.dateTimeProvider.GetTimeOffset(),
				Network = this.network,
				AccountsRoot = new List<AccountRoot> { new AccountRoot() { Accounts = new List<GeneralPurposeAccount>(), CoinType = this.coinType } },
			};

			// create a folder if none exists and persist the file
			this.fileStorage.SaveToFile(walletFile, $"{name}.{WalletFileExtension}");

			this.logger.LogTrace("(-)");
			return walletFile;
		}

		/// <summary>
		/// Loads the wallet to be used by the manager.
		/// </summary>
		/// <param name="wallet">The wallet to load.</param>
		private void Load(GeneralPurposeWallet wallet)
		{
			Guard.NotNull(wallet, nameof(wallet));
			this.logger.LogTrace("({0}:'{1}')", nameof(wallet), wallet.Name);

			if (this.Wallets.Any(w => w.Name == wallet.Name))
			{
				this.logger.LogTrace("(-)[NOT_FOUND]");
				return;
			}

			this.Wallets.Add(wallet);
			this.logger.LogTrace("(-)");
		}

		/// <summary>
		/// Loads the keys and transactions we're tracking in memory for faster lookups.
		/// </summary>
		public void LoadKeysLookupLock()
		{
			lock (this.lockObject)
			{
				var lookup = new Dictionary<Script, GeneralPurposeAddress>();
				foreach (var wallet in this.Wallets)
				{
					var accounts = wallet.GetAccountsByCoinType(this.coinType);
					foreach (var account in accounts)
					{
						var addresses = account.ExternalAddresses.Concat(account.InternalAddresses);
						foreach (var address in addresses)
						{
							lookup.Add(address.ScriptPubKey, address);
							if (address.Pubkey != null)
								lookup.Add(address.Pubkey, address);
						}
					}
				}

				this.keysLookup = lookup;
			}
		}

		/// <summary>
		/// Loads the keys and transactions we're tracking for the multisig addresses in memory for faster lookups.
		/// </summary>
		public void LoadMultiSigKeysLookupLock()
		{
			lock (this.lockObject)
			{
				var lookup = new Dictionary<Script, MultiSigAddress>();
				foreach (GeneralPurposeWallet wallet in this.Wallets)
				{
					var accounts = wallet.GetAccountsByCoinType(this.coinType);
					foreach (GeneralPurposeAccount account in accounts)
					{
						var addresses = account.MultiSigAddresses;
						foreach (MultiSigAddress address in addresses)
						{
							if (address.ScriptPubKey != null)
								lookup.Add(address.ScriptPubKey, address);
						}
					}
				}

				this.multiSigKeysLookup = lookup;
			}
		}

		/// <inheritdoc />
		public IEnumerable<string> GetWalletsNames()
		{
			return this.Wallets.Select(w => w.Name);
		}

		/// <inheritdoc />
		public GeneralPurposeWallet GetWalletByName(string walletName)
		{
			this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

			GeneralPurposeWallet wallet = this.Wallets.SingleOrDefault(w => w.Name == walletName);
			if (wallet == null)
			{
				this.logger.LogTrace("(-)[NOT_FOUND]");
				throw new GeneralPurposeWalletException($"No wallet with name {walletName} could be found.");
			}

			this.logger.LogTrace("(-)");
			return wallet;
		}

		/// <inheritdoc />
		public ICollection<uint256> GetFirstWalletBlockLocator()
		{
			return this.Wallets.First().BlockLocator;
		}

		/// <inheritdoc />
		public int? GetEarliestWalletHeight()
		{
			return this.Wallets.Min(w => w.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight);
		}

		/// <inheritdoc />
		public DateTimeOffset GetOldestWalletCreationTime()
		{
			return this.Wallets.Min(w => w.CreationTime);
		}

		/// <summary>
		/// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
		/// </summary>
		/// <param name="wallets">The wallets to update when the chain has downloaded.</param>
		/// <param name="date">The creation date of the block with which to update the wallet.</param>
		private void UpdateWhenChainDownloaded(IEnumerable<GeneralPurposeWallet> wallets, DateTime date)
		{
			this.asyncLoopFactory.RunUntil("WalletManager.DownloadChain", this.nodeLifetime.ApplicationStopping,
				() => this.chain.IsDownloaded(),
				() =>
				{
					int heightAtDate = this.chain.GetHeightAtTime(date);

					foreach (var wallet in wallets)
					{
						this.logger.LogTrace("The chain of headers has finished downloading, updating wallet '{0}' with height {1}", wallet.Name, heightAtDate);
						this.UpdateLastBlockSyncedHeight(wallet, this.chain.GetBlock(heightAtDate));
						this.SaveWallet(wallet);
					}
				},
				(ex) =>
				{
					// in case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
					// sync from the current height.
					this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");

					foreach (var wallet in wallets)
					{
						this.UpdateLastBlockSyncedHeight(wallet, this.chain.Tip);
					}
				},
				TimeSpans.FiveSeconds);
		}

        public Transaction SignPartialTransaction(Transaction partial, ICollection<MultiSigAddress> multiSigAddresses)
        {
            // Find which multisig address is being referred to by the inputs
            // TODO: Require this to be passed in as a parameter to save the lookup?

            MultiSigAddress multiSigAddress = null;

            foreach (MultiSigAddress address in multiSigAddresses)
            {
                foreach (TransactionData tx in address.Transactions)
                {
                    foreach (var input in partial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id)
                        {
                            multiSigAddress = address;
                        }
                    }
                }
            }

            if (multiSigAddress == null)
                throw new GeneralPurposeWalletException(
                    "Unable to determine which multisig address to combine partial transactions for");

            // Need to get the same ScriptCoins used by the other signatories.
            // It is assumed that the funds are present in the MultiSigAddress
            // transactions.

            // Find the transaction(s) in the MultiSigAddress that have the
            // referenced inputs among their outputs.

            List<Transaction> fundingTransactions = new List<Transaction>();

            foreach (TransactionData tx in multiSigAddress.Transactions)
            {
                foreach (var output in tx.Transaction.Outputs.AsIndexedOutputs())
                {
                    foreach (var input in partial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id && input.PrevOut.N == output.N)
                            fundingTransactions.Add(tx.Transaction);
                    }
                }
            }

            // Then convert the outputs to Coins & make ScriptCoins out of them.

            List<ScriptCoin> scriptCoins = new List<ScriptCoin>();

            foreach (var tx in fundingTransactions)
            {
                foreach (var coin in tx.Outputs.AsCoins())
                {
                    // Only care about outputs for our particular multisig
                    if (coin.ScriptPubKey == multiSigAddress.ScriptPubKey)
                    {
                        scriptCoins.Add(coin.ToScriptCoin(multiSigAddress.RedeemScript));
                    }
                }
            }

            // Need to construct a transaction using a transaction builder with
            // the appropriate state

            TransactionBuilder builder = new TransactionBuilder(this.network);

            Transaction signed =
                builder
                    .AddCoins(scriptCoins)
                    .AddKeys(multiSigAddress.PrivateKey)
                    .SignTransaction(partial);

            return signed;
        }

        public Transaction CombinePartialTransactions(Transaction[] partials, ICollection<MultiSigAddress> multiSigAddresses)
        {
            Transaction firstPartial = partials[0];

            // Find which multisig address is being referred to by the inputs
            // TODO: Require this to be passed in as a parameter to save the lookup?

            MultiSigAddress multiSigAddress = null;

            foreach (MultiSigAddress address in multiSigAddresses)
            {
                foreach (TransactionData tx in address.Transactions)
                {
                    foreach (var input in firstPartial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id)
                        {
                            multiSigAddress = address;
                        }
                    }
                }
            }

            if (multiSigAddress == null)
                throw new GeneralPurposeWalletException(
                    "Unable to determine which multisig address to combine partial transactions for");

            // Need to get the same ScriptCoins used by the other signatories.
            // It is assumed that the funds are present in the MultiSigAddress
            // transactions.

            // Find the transaction(s) in the MultiSigAddress that have the
            // referenced inputs among their outputs.

            List<Transaction> fundingTransactions = new List<Transaction>();

            foreach (TransactionData tx in multiSigAddress.Transactions)
            {
                foreach (var output in tx.Transaction.Outputs.AsIndexedOutputs())
                {
                    foreach (var input in firstPartial.Inputs)
                    {
                        if (input.PrevOut.Hash == tx.Id && input.PrevOut.N == output.N)
                            fundingTransactions.Add(tx.Transaction);
                    }
                }
            }

            // Then convert the outputs to Coins & make ScriptCoins out of them.

            List<ScriptCoin> scriptCoins = new List<ScriptCoin>();

            foreach (var tx in fundingTransactions)
            {
                foreach (var coin in tx.Outputs.AsCoins())
                {
                    // Only care about outputs for our particular multisig
                    if (coin.ScriptPubKey == multiSigAddress.ScriptPubKey)
                    {
                        scriptCoins.Add(coin.ToScriptCoin(multiSigAddress.RedeemScript));
                    }
                }
            }

            // Need to construct a transaction using a transaction builder with
            // the appropriate state

            TransactionBuilder builder = new TransactionBuilder(this.network);

            Transaction combined =
                builder
                    .AddCoins(scriptCoins)
                    .CombineSignatures(partials);

            return combined;
        }
    }
}
