using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet
{
	/// <summary>
	/// A wallet containing addresses not derived from an HD seed.
	/// Also sometimes referred to as a JBOK (Just a Bunch Of Keys)
	/// wallet.
	/// </summary>
	public class GeneralPurposeWallet
	{
		/// <summary>
		/// Initializes a new instance of the general purpose wallet.
		/// </summary>
		public GeneralPurposeWallet()
		{
			this.AccountsRoot = new List<AccountRoot>();
		}

		/// <summary>
		/// The name of this wallet.
		/// </summary>
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the merkle path.
		/// </summary>
		[JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
		public ICollection<uint256> BlockLocator { get; set; }

		/// <summary>
		/// The network this wallet contains addresses and transactions for.
		/// </summary>
		[JsonProperty(PropertyName = "network")]
		[JsonConverter(typeof(NetworkConverter))]
		public Network Network { get; set; }

		/// <summary>
		/// The time this wallet was created.
		/// </summary>
		[JsonProperty(PropertyName = "creationTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// The root of the accounts tree.
		/// </summary>
		[JsonProperty(PropertyName = "accountsRoot")]
		public ICollection<AccountRoot> AccountsRoot { get; set; }

		public GeneralPurposeAddress GetAddressFromBase58(string base58)
		{
			foreach (GeneralPurposeAccount account in this.GetAccountsByCoinType((CoinType)this.Network.Consensus.CoinType))
			{
				var address = account.GetAddressFromBase58(base58);

				if (address != null)
					return address;
			}

			return null;
		}

		/// <summary>
		/// Gets the accounts the wallet has for this type of coin.
		/// </summary>
		/// <param name="coinType">Type of the coin.</param>
		/// <returns>The accounts in the wallet corresponding to this type of coin.</returns>
		public IEnumerable<GeneralPurposeAccount> GetAccountsByCoinType(CoinType coinType)
		{
			return this.AccountsRoot.Where(a => a.CoinType == coinType).SelectMany(a => a.Accounts);
		}

		/// <summary>
		/// Gets an account from the wallet's accounts.
		/// </summary>
		/// <param name="accountName">The name of the account to retrieve.</param>
		/// <param name="coinType">The type of the coin this account is for.</param>
		/// <returns>The requested account.</returns>
		public GeneralPurposeAccount GetAccountByCoinType(string accountName, CoinType coinType)
		{
			AccountRoot accountRoot = this.AccountsRoot.SingleOrDefault(a => a.CoinType == coinType);
			return accountRoot?.GetAccountByName(accountName);
		}

		/// <summary>
		/// Gets all the transactions by coin type.
		/// </summary>
		/// <param name="coinType">Type of the coin.</param>
		/// <returns></returns>
		public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType coinType)
		{
			var accounts = this.GetAccountsByCoinType(coinType).ToList();

			foreach (TransactionData txData in accounts.SelectMany(x => x.ExternalAddresses).SelectMany(x => x.Transactions))
			{
				yield return txData;
			}

			foreach (TransactionData txData in accounts.SelectMany(x => x.InternalAddresses).SelectMany(x => x.Transactions))
			{
				yield return txData;
			}
		}

		/// <summary>
		/// Gets all the pub keys contained in this wallet.
		/// </summary>
		/// <param name="coinType">Type of the coin.</param>
		/// <returns></returns>
		public IEnumerable<Script> GetAllPubKeysByCoinType(CoinType coinType)
		{
			var accounts = this.GetAccountsByCoinType(coinType).ToList();

			foreach (Script script in accounts.SelectMany(x => x.ExternalAddresses).Select(x => x.ScriptPubKey))
			{
				yield return script;
			}

			foreach (Script script in accounts.SelectMany(x => x.InternalAddresses).Select(x => x.ScriptPubKey))
			{
				yield return script;
			}
		}

		/// <summary>
		/// Adds an account to the current wallet.
		/// </summary>
		/// <param name="name">The name of the account.</param>
		/// <param name="coinType">The type of coin this account is for.</param>
		/// <param name="accountCreationTime">Creation time of the account to be created.</param>
		/// <returns>A new HD account.</returns>
		public GeneralPurposeAccount AddNewAccount(string name, CoinType coinType, DateTimeOffset accountCreationTime)
		{
			Guard.NotEmpty(name, nameof(name));

			AccountRoot accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
			return accountRoot.AddNewAccount(name, this.Network, accountCreationTime);
		}

		/// <summary>
		/// Gets the first account that contains no transaction.
		/// </summary>
		/// <returns>An unused account.</returns>
		public GeneralPurposeAccount GetFirstUnusedAccount(CoinType coinType)
		{
			// Get the accounts root for this type of coin.
			var accountsRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);

			if (accountsRoot.Accounts.Any())
			{
				// Get an unused account.
				var firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
				if (firstUnusedAccount != null)
				{
					return firstUnusedAccount;
				}
			}

			return null;
		}

		/// <summary>
		/// Determines whether the wallet contains the specified address.
		/// </summary>
		/// <param name="address">The address to check.</param>
		/// <returns>A value indicating whether the wallet contains the specified address.</returns>
		public bool ContainsAddress(GeneralPurposeAddress address)
		{
			if (!this.AccountsRoot.Any(r => r.Accounts.Any(
				a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
					 a.InternalAddresses.Any(i => i.Address == address.Address))))
			{
				return false;
			}

			return true;
		}

		// TODO: The private key is not encrypted yet.
		/// <summary>
		/// Gets the extended private key for the given address.
		/// </summary>
		/// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
		/// <param name="address">The address to get the private key for.</param>
		/// <returns>The private key.</returns>
		public Key GetPrivateKeyForAddress(string password, GeneralPurposeAddress address)
		{
			Guard.NotEmpty(password, nameof(password));
			Guard.NotNull(address, nameof(address));

			// Check if the wallet contains the address.
			if (!this.ContainsAddress(address))
			{
				throw new GeneralPurposeWalletException("Address not found in wallet.");
			}
			
			return address.PrivateKey;
		}

		/// <summary>
		/// Lists all spendable transactions from all accounts in the wallet.
		/// </summary>
		/// <param name="coinType">Type of the coin to get transactions from.</param>
		/// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
		/// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
		/// <returns>A collection of spendable outputs.</returns>
		public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(CoinType coinType, int currentChainHeight, int confirmations = 0)
		{
			IEnumerable<GeneralPurposeAccount> accounts = this.GetAccountsByCoinType(coinType);

			return accounts
				.SelectMany(x => x.GetSpendableTransactions(currentChainHeight, confirmations));
		}
	}

	/// <summary>
	/// The root for the accounts for any type of coins.
	/// </summary>
	public class AccountRoot
	{
		/// <summary>
		/// Initializes a new instance of the object.
		/// </summary>
		public AccountRoot()
		{
			this.Accounts = new List<GeneralPurposeAccount>();
		}

		/// <summary>
		/// The type of coin, Bitcoin or Stratis.
		/// </summary>
		[JsonProperty(PropertyName = "coinType")]
		public CoinType CoinType { get; set; }

		/// <summary>
		/// The height of the last block that was synced.
		/// </summary>
		[JsonProperty(PropertyName = "lastBlockSyncedHeight", NullValueHandling = NullValueHandling.Ignore)]
		public int? LastBlockSyncedHeight { get; set; }

		/// <summary>
		/// The hash of the last block that was synced.
		/// </summary>
		[JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 LastBlockSyncedHash { get; set; }

		/// <summary>
		/// The accounts used in the wallet.
		/// </summary>
		[JsonProperty(PropertyName = "accounts")]
		public ICollection<GeneralPurposeAccount> Accounts { get; set; }

		/// <summary>
		/// Gets the first account that contains no transaction.
		/// </summary>
		/// <returns>An unused account</returns>
		public GeneralPurposeAccount GetFirstUnusedAccount()
		{
			if (this.Accounts == null)
				return null;

			var unusedAccounts = this.Accounts.Where(acc => !acc.ExternalAddresses.Any() && !acc.InternalAddresses.Any()).ToList();
			if (!unusedAccounts.Any())
				return null;

			return unusedAccounts.First();
		}

		/// <summary>
		/// Gets the account matching the name passed as a parameter.
		/// </summary>
		/// <param name="accountName">The name of the account to get.</param>
		/// <returns></returns>
		/// <exception cref="System.Exception"></exception>
		public GeneralPurposeAccount GetAccountByName(string accountName)
		{
			if (this.Accounts == null)
				throw new GeneralPurposeWalletException($"No account with the name {accountName} could be found.");

			// get the account
			GeneralPurposeAccount account = this.Accounts.SingleOrDefault(a => a.Name == accountName);
			if (account == null)
				throw new GeneralPurposeWalletException($"No account with the name {accountName} could be found.");

			return account;
		}

		/// <summary>
		/// Adds an account to the current account root.
		/// </summary>
		/// <param name="name">The name of the account.</param>
		/// <param name="network">The network for which this account will be created.</param>
		/// <param name="accountCreationTime">Creation time of the account to be created.</param>
		/// <returns>A new general purpose account that can contain non-HD addresses.</returns>
		public GeneralPurposeAccount AddNewAccount(string name, Network network, DateTimeOffset accountCreationTime)
		{
			Guard.NotEmpty(name, nameof(name));

			// Get the current collection of accounts.
			List<GeneralPurposeAccount> accounts = this.Accounts.ToList();

			var newAccount = new GeneralPurposeAccount
			{
				ExternalAddresses = new List<GeneralPurposeAddress>(),
				InternalAddresses = new List<GeneralPurposeAddress>(),
				Name = name,
				CreationTime = accountCreationTime,
				CoinType = this.CoinType
			};

			accounts.Add(newAccount);
			this.Accounts = accounts;

			return newAccount;
		}
	}

	/// <summary>
	/// A general purpose account's details.
	/// </summary>
	public class GeneralPurposeAccount
	{
		public GeneralPurposeAccount()
		{
			this.ExternalAddresses = new List<GeneralPurposeAddress>();
			this.InternalAddresses = new List<GeneralPurposeAddress>();
			this.MultiSigAddresses = new List<MultiSigAddress>();
		}

		/// <summary>
		/// The name of this account.
		/// </summary>
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the creation time.
		/// </summary>
		[JsonProperty(PropertyName = "creationTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// The list of external addresses, typically used for receiving money.
		/// </summary>
		[JsonProperty(PropertyName = "externalAddresses")]
		public ICollection<GeneralPurposeAddress> ExternalAddresses { get; set; }

		/// <summary>
		/// The list of internal addresses, typically used to receive change.
		/// </summary>
		[JsonProperty(PropertyName = "internalAddresses")]
		public ICollection<GeneralPurposeAddress> InternalAddresses { get; set; }

		/// <summary>
		/// The list of multisig addresses, where this node is one of several signatories to transactions.
		/// </summary>
		[JsonProperty(PropertyName = "multiSigAddresses")]
		public ICollection<MultiSigAddress> MultiSigAddresses { get; set; }

		/// <summary>
		/// The coin type this account is used for.
		/// </summary>
		[JsonProperty(PropertyName = "coinType")]
		public CoinType CoinType { get; set; }

		/// <summary>
		/// Gets the type of coin this account is for.
		/// </summary>
		/// <returns>A <see cref="CoinType"/>.</returns>
		public CoinType GetCoinType()
		{
			return this.CoinType;
		}

		/// <summary>
		/// Adds the key and address information from a MultiSigAddress into the account's
		/// multisig address list. Future transactions affecting this address will then
		/// automatically be processed and recorded by the wallet.
		/// </summary>
		/// <param name="multiSigAddress">The MultiSigAddress to import into the address list.</param>
		public bool ImportMultiSigAddress(MultiSigAddress multiSigAddress)
		{
			if (!this.MultiSigAddresses.Contains(multiSigAddress))
			{
				this.MultiSigAddresses.Add(multiSigAddress);

				// Indicate that the address was successfully added to the account
				return true;
			}

			// The address was already imported
			return false;
		}

		public Transaction SignPartialTransaction(Transaction partial)
		{
			// Find which multisig address is being referred to by the inputs
			// TODO: Require this to be passed in as a parameter to save the lookup?

			MultiSigAddress multiSigAddress = null;

			foreach (MultiSigAddress address in this.MultiSigAddresses)
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

			TransactionBuilder builder = new TransactionBuilder();

			Transaction signed =
				builder
					.AddCoins(scriptCoins)
					.AddKeys(multiSigAddress.PrivateKey)
					.SignTransaction(partial);

			return signed;
		}

		public Transaction CombinePartialTransactions(Transaction[] partials)
		{
			Transaction firstPartial = partials[0];

			// Find which multisig address is being referred to by the inputs
			// TODO: Require this to be passed in as a parameter to save the lookup?

			MultiSigAddress multiSigAddress = null;

			foreach (MultiSigAddress address in this.MultiSigAddresses)
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

			TransactionBuilder builder = new TransactionBuilder();

			Transaction combined =
				builder
					.AddCoins(scriptCoins)
					.CombineSignatures(partials);

			return combined;
		}

		/// <summary>
		/// Gets the first receiving address that contains no transactions.
		/// </summary>
		/// <returns>An unused address</returns>
		public GeneralPurposeAddress GetFirstUnusedReceivingAddress()
		{
			return this.GetFirstUnusedAddress(false);
		}

		/// <summary>
		/// Gets the first change address that contains no transactions.
		/// </summary>
		/// <returns>An unused address</returns>
		public GeneralPurposeAddress GetFirstUnusedChangeAddress()
		{
			return this.GetFirstUnusedAddress(true);
		}

		/// <summary>
		/// Gets the first receiving address that contains no transaction.
		/// </summary>
		/// <returns>An unused address</returns>
		private GeneralPurposeAddress GetFirstUnusedAddress(bool isChange)
		{
			IEnumerable<GeneralPurposeAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
			if (addresses == null)
				return null;

			List<GeneralPurposeAddress> unusedAddresses = addresses.Where(acc => !acc.Transactions.Any()).ToList();
			if (!unusedAddresses.Any())
			{
				return null;
			}

			return unusedAddresses.First();
		}

		public GeneralPurposeAddress GetAddressFromBase58(string base58)
		{
			foreach (GeneralPurposeAddress address in this.ExternalAddresses)
			{
				if (address.Address.Equals(base58))
					return address;
			}

			foreach (GeneralPurposeAddress address in this.InternalAddresses)
			{
				if (address.Address.Equals(base58))
					return address;
			}

			return null;
		}

		public MultiSigAddress GetMultiSigAddressFromBase58(string base58)
		{
			foreach (MultiSigAddress address in this.MultiSigAddresses)
			{
				if (address.Address.Equals(base58))
					return address;
			}

			return null;
		}

		/// <summary>
		/// Gets a collection of transactions by id.
		/// </summary>
		/// <param name="id">The identifier.</param>
		/// <returns></returns>
		public IEnumerable<TransactionData> GetTransactionsById(uint256 id)
		{
			Guard.NotNull(id, nameof(id));

			var addresses = this.GetCombinedAddresses();
			return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.Id == id));
		}

		/// <summary>
		/// Gets a collection of transactions with spendable outputs.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<TransactionData> GetSpendableTransactions()
		{
			var addresses = this.GetCombinedAddresses();
			return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.IsSpendable()));
		}

		/// <summary>
		/// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
		/// </summary>
		public (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount(bool multiSig = false)
		{
			List<TransactionData> allTransactions;

			if (!multiSig)
			{
				allTransactions = this.ExternalAddresses.SelectMany(a => a.Transactions)
					.Concat(this.InternalAddresses.SelectMany(i => i.Transactions)).ToList();
			}
			else
			{
				allTransactions = this.MultiSigAddresses.SelectMany(a => a.Transactions).ToList();
			}

			var confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
			var total = allTransactions.Sum(t => t.SpendableAmount(false));

			return (confirmed, total - confirmed);
		}

		/// <summary>
		/// Get the total spendable value for both confirmed and unconfirmed UTXO for one multisig address.
		/// </summary>
		public (Money ConfirmedAmount, Money UnConfirmedAmount) GetMultiSigAddressSpendableAmount(string address)
		{
			MultiSigAddress multiSigAddress = this.MultiSigAddresses.Where(a => a.Address == address).FirstOrDefault();
			List<TransactionData> allTransactions = multiSigAddress.UnspentTransactions().ToList();

			var confirmed = allTransactions.Sum(t => t.SpendableAmount(true));
			var total = allTransactions.Sum(t => t.SpendableAmount(false));

			return (confirmed, total - confirmed);
		}

		/// <summary>
		/// Finds the addresses in which a transaction is contained.
		/// </summary>
		/// <remarks>
		/// Returns a collection because a transaction can be contained in a change address as well as in a receive address (as a spend).
		/// </remarks>
		/// <param name="predicate">A predicate by which to filter the transactions.</param>
		/// <returns></returns>
		public IEnumerable<GeneralPurposeAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate)
		{
			Guard.NotNull(predicate, nameof(predicate));

			var addresses = this.GetCombinedAddresses();
			return addresses.Where(t => t.Transactions != null).Where(a => a.Transactions.Any(predicate));
		}

		/// <summary>
		/// Return both the external and internal (change) address from an account.
		/// </summary>
		/// <returns>All addresses that belong to this account.</returns>
		public IEnumerable<GeneralPurposeAddress> GetCombinedAddresses()
		{
			IEnumerable<GeneralPurposeAddress> addresses = new List<GeneralPurposeAddress>();
			if (this.ExternalAddresses != null)
			{
				addresses = this.ExternalAddresses;
			}

			if (this.InternalAddresses != null)
			{
				addresses = addresses.Concat(this.InternalAddresses);
			}

			return addresses;
		}

		/// <summary>
		/// Creates a number of additional addresses in the current account.
		/// </summary>
		/// <param name="network">The network these addresses will be for.</param>
		/// <param name="addressesQuantity">The number of addresses to create.</param>
		/// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
		/// <returns>A list of addresses in Base58 format.</returns>
		public List<string> CreateAddresses(Network network, int addressesQuantity, bool isChange = false)
		{
			List<string> addressesCreated = new List<string>();

			var addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;

			for (int i = 0; i < addressesQuantity; i++)
			{
				// Generate a new address.
				Key privateKey = new Key();
				PubKey pubkey = privateKey.PubKey;
				BitcoinPubKeyAddress address = pubkey.GetAddress(network);

				// Add the new address details to the list of addresses.
				addresses.Add(new GeneralPurposeAddress
				{
					PrivateKey = privateKey,
					ScriptPubKey = address.ScriptPubKey,
					Pubkey = pubkey.ScriptPubKey,
					Address = address.ToString(),
					Transactions = new List<TransactionData>(),
					IsChangeAddress = isChange
				});

				addressesCreated.Add(address.ToString());
			}

			if (isChange)
			{
				this.InternalAddresses = addresses;
			}
			else
			{
				this.ExternalAddresses = addresses;
			}

			return addressesCreated;
		}

		/// <summary>
		/// Lists all spendable transactions in the current account.
		/// </summary>
		/// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
		/// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
		/// <returns>A collection of spendable outputs that belong to the given account.</returns>
		public IEnumerable<UnspentOutputReference> GetSpendableTransactions(int currentChainHeight, int confirmations = 0)
		{
			// This will take all the spendable coins that belong to the account and keep the reference to the GeneralPurposeAddress and GeneralPurposeAccount.
			// This is useful so later the private key can be calculated just from a given UTXO.
			foreach (GeneralPurposeAddress address in this.GetCombinedAddresses())
			{
				// A block that is at the tip has 1 confirmation.
				// When calculating the confirmations the tip must be advanced by one.

				int countFrom = currentChainHeight + 1;
				foreach (TransactionData transactionData in address.UnspentTransactions())
				{
					int? confirmationCount = 0;
					if (transactionData.BlockHeight != null)
						confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

					if (confirmationCount >= confirmations)
					{
						yield return new UnspentOutputReference
						{
							Account = this,
							Address = address,
							Transaction = transactionData,
						};
					}
				}
			}
		}

		/// <summary>
		/// Lists all spendable transactions in the current account.
		/// </summary>
		/// <param name="currentChainHeight">The current height of the chain. Used for calculating the number of confirmations a transaction has.</param>
		/// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
		/// <returns>A collection of spendable outputs that belong to the given account.</returns>
		public IEnumerable<UnspentMultiSigOutputReference> GetSpendableMultiSigTransactions(Script scriptPubKey, int currentChainHeight, int confirmations = 0)
		{
			// This will take all the spendable coins that belong to the account and keep the reference to the MultiSigAddress and GeneralPurposeAccount.
			// This is useful so later the private key can be retrieved just from a given UTXO.
			foreach (MultiSigAddress address in this.MultiSigAddresses)
			{
				// A block that is at the tip has 1 confirmation.
				// When calculating the confirmations the tip must be advanced by one.

				int countFrom = currentChainHeight + 1;
				foreach (TransactionData transactionData in address.UnspentTransactions())
				{
					int? confirmationCount = 0;
					if (transactionData.BlockHeight != null)
						confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

					if (confirmationCount >= confirmations)
					{
						yield return new UnspentMultiSigOutputReference
						{
							Account = this,
							Address = address,
							Transaction = transactionData,
						};
					}
				}
			}
		}
	}

	/// <summary>
	/// A general purpose address; not derived from an HD seed.
	/// </summary>
	public class GeneralPurposeAddress
	{
		public GeneralPurposeAddress()
		{
			this.Transactions = new List<TransactionData>();
		}
		
		// TODO: Encrypt the private key with the wallet password
		/// <summary>
		/// The private key for this address.
		/// </summary>
		[JsonProperty(PropertyName = "privateKey")]
		[JsonConverter(typeof(KeyJsonConverter))]
		public Key PrivateKey { get; set; }

		/// <summary>
		/// The script pub key for this address.
		/// </summary>
		[JsonProperty(PropertyName = "scriptPubKey")]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script ScriptPubKey { get; set; }

		/// <summary>
		/// The script pub key for this address.
		/// </summary>
		[JsonProperty(PropertyName = "pubkey")]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script Pubkey { get; set; }

		/// <summary>
		/// The Base58 representation of this address.
		/// </summary>
		[JsonProperty(PropertyName = "address")]
		public string Address { get; set; }

		/// <summary>
		/// A list of transactions involving this address.
		/// </summary>
		[JsonProperty(PropertyName = "transactions")]
		public ICollection<TransactionData> Transactions { get; set; }

		/// <summary>
		/// Whether or not this address is considered a change address.
		/// </summary>
		[JsonProperty(PropertyName = "isChangeAddress")]
		public bool IsChangeAddress { get; set; }

		/// <summary>
		/// List all spendable transactions in an address.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<TransactionData> UnspentTransactions()
		{
			if (this.Transactions == null)
			{
				return new List<TransactionData>();
			}

			return this.Transactions.Where(t => t.IsSpendable());
		}
	}

	/// <summary>
	/// An object containing transaction data.
	/// </summary>
	public class TransactionData
	{
		/// <summary>
		/// Transaction id.
		/// </summary>
		[JsonProperty(PropertyName = "id")]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 Id { get; set; }

		/// <summary>
		/// The transaction amount.
		/// </summary>
		[JsonProperty(PropertyName = "amount")]
		[JsonConverter(typeof(MoneyJsonConverter))]
		public Money Amount { get; set; }

		/// <summary>
		/// A value indicating whether this is a coin stake transaction or not.
		/// </summary>
		[JsonProperty(PropertyName = "isCoinStake", NullValueHandling = NullValueHandling.Ignore)]
		public bool? IsCoinStake { get; set; }

		/// <summary>
		/// The index of this scriptPubKey in the transaction it is contained.
		/// </summary>
		/// <remarks>
		/// This is effectively the index of the output, the position of the output in the parent transaction.
		/// </remarks>
		[JsonProperty(PropertyName = "index", NullValueHandling = NullValueHandling.Ignore)]
		public int Index { get; set; }

		/// <summary>
		/// The height of the block including this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
		public int? BlockHeight { get; set; }

		/// <summary>
		/// The hash of the block including this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "blockHash", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 BlockHash { get; set; }

		/// <summary>
		/// Gets or sets the creation time.
		/// </summary>
		[JsonProperty(PropertyName = "creationTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// Gets or sets the Merkle proof for this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "merkleProof", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(BitcoinSerializableJsonConverter))]
		public PartialMerkleTree MerkleProof { get; set; }

		/// <summary>
		/// The script pub key for this address.
		/// </summary>
		[JsonProperty(PropertyName = "scriptPubKey")]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script ScriptPubKey { get; set; }

		/// <summary>
		/// Hexadecimal representation of this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
		public string Hex { get; set; }

		/// <summary>
		/// Propagation state of this transaction.
		/// </summary>
		/// <remarks>Assume it's <c>true</c> if the field is <c>null</c>.</remarks>
		[JsonProperty(PropertyName = "isPropagated", NullValueHandling = NullValueHandling.Ignore)]
		public bool? IsPropagated { get; set; }

		/// <summary>
		/// Gets or sets the full transaction object.
		/// </summary>
		[JsonIgnore]
		public Transaction Transaction => Transaction.Parse(this.Hex);

		/// <summary>
		/// The details of the transaction in which the output referenced in this transaction is spent.
		/// </summary>
		[JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
		public SpendingDetails SpendingDetails { get; set; }

		/// <summary>
		/// Determines whether this transaction is confirmed.
		/// </summary>
		public bool IsConfirmed()
		{
			return this.BlockHeight != null;
		}

		/// <summary>
		/// Indicates an output is spendable.
		/// </summary>
		public bool IsSpendable()
		{
			// TODO: Coinbase maturity check?
			return this.SpendingDetails == null;
		}

		public Money SpendableAmount(bool confirmedOnly)
		{
			// This method only returns a UTXO that has no spending output.
			// If a spending output exists (even if its not confirmed) this will return as zero balance.
			if (this.IsSpendable())
			{
				// If the 'confirmedOnly' flag is set check that the UTXO is confirmed.
				if (confirmedOnly && !this.IsConfirmed())
				{
					return Money.Zero;
				}

				return this.Amount;
			}

			return Money.Zero;
		}
	}

	/// <summary>
	/// An object representing a payment.
	/// </summary>
	public class PaymentDetails
	{
		/// <summary>
		/// The script pub key of the destination address.
		/// </summary>
		[JsonProperty(PropertyName = "destinationScriptPubKey")]
		[JsonConverter(typeof(ScriptJsonConverter))]
		public Script DestinationScriptPubKey { get; set; }

		/// <summary>
		/// The Base58 representation of the destination  address.
		/// </summary>
		[JsonProperty(PropertyName = "destinationAddress")]
		public string DestinationAddress { get; set; }

		/// <summary>
		/// The transaction amount.
		/// </summary>
		[JsonProperty(PropertyName = "amount")]
		[JsonConverter(typeof(MoneyJsonConverter))]
		public Money Amount { get; set; }
	}

	public class SpendingDetails
	{
		public SpendingDetails()
		{
			this.Payments = new List<PaymentDetails>();
		}

		/// <summary>
		/// The id of the transaction in which the output referenced in this transaction is spent.
		/// </summary>
		[JsonProperty(PropertyName = "transactionId", NullValueHandling = NullValueHandling.Ignore)]
		[JsonConverter(typeof(UInt256JsonConverter))]
		public uint256 TransactionId { get; set; }

		/// <summary>
		/// A list of payments made out in this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
		public ICollection<PaymentDetails> Payments { get; set; }

		/// <summary>
		/// The height of the block including this transaction.
		/// </summary>
		[JsonProperty(PropertyName = "blockHeight", NullValueHandling = NullValueHandling.Ignore)]
		public int? BlockHeight { get; set; }

		/// <summary>
		/// A value indicating whether this is a coin stake transaction or not.
		/// </summary>
		[JsonProperty(PropertyName = "isCoinStake", NullValueHandling = NullValueHandling.Ignore)]
		public bool? IsCoinStake { get; set; }

		/// <summary>
		/// Gets or sets the creation time.
		/// </summary>
		[JsonProperty(PropertyName = "creationTime")]
		[JsonConverter(typeof(DateTimeOffsetConverter))]
		public DateTimeOffset CreationTime { get; set; }

		/// <summary>
		/// Hexadecimal representation of this spending transaction.
		/// </summary>
		[JsonProperty(PropertyName = "hex", NullValueHandling = NullValueHandling.Ignore)]
		public string Hex { get; set; }

		/// <summary>
		/// Gets or sets the full transaction object.
		/// </summary>
		[JsonIgnore]
		public Transaction Transaction => Transaction.Parse(this.Hex);

		/// <summary>
		/// Determines whether this transaction being spent is confirmed.
		/// </summary>
		public bool IsSpentConfirmed()
		{
			return this.BlockHeight != null;
		}
	}

	/// <summary>
	/// Represents an UTXO that keeps a reference to <see cref="GeneralPurposeAddress"/> and <see cref="GeneralPurposeAccount"/>.
	/// </summary>
	public class UnspentOutputReference
	{
		/// <summary>
		/// The account associated with this UTXO
		/// </summary>
		public GeneralPurposeAccount Account { get; set; }

		/// <summary>
		/// The address associated with this UTXO
		/// </summary>
		public GeneralPurposeAddress Address { get; set; }

		/// <summary>
		/// The transaction representing the UTXO.
		/// </summary>
		public TransactionData Transaction { get; set; }

		/// <summary>
		/// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
		/// </summary>
		/// <returns>The corresponding <see cref="OutPoint"/>.</returns>
		public OutPoint ToOutPoint()
		{
			return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
		}
	}

	/// <summary>
	/// Represents an UTXO that keeps a reference to <see cref="MultiSigAddress"/> and <see cref="GeneralPurposeAccount"/>.
	/// </summary>
	public class UnspentMultiSigOutputReference
	{
		/// <summary>
		/// The account associated with this UTXO
		/// </summary>
		public GeneralPurposeAccount Account { get; set; }

		/// <summary>
		/// The address associated with this UTXO
		/// </summary>
		public MultiSigAddress Address { get; set; }

		/// <summary>
		/// The transaction representing the UTXO.
		/// </summary>
		public TransactionData Transaction { get; set; }

		/// <summary>
		/// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
		/// </summary>
		/// <returns>The corresponding <see cref="OutPoint"/>.</returns>
		public OutPoint ToOutPoint()
		{
			return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
		}
	}
}
