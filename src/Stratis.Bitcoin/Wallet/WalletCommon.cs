using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;
using TracerAttributes;

namespace Stratis.Bitcoin.Wallet
{
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
            this.Accounts = new List<HdAccount>();
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
        public ICollection<HdAccount> Accounts { get; set; }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account</returns>
        public HdAccount GetFirstUnusedAccount()
        {
            if (this.Accounts == null)
                return null;

            List<HdAccount> unusedAccounts = this.Accounts.Where(a => a.Index < 100_000_000).Where(acc => !acc.ExternalAddresses.SelectMany(add => add.Transactions).Any() && !acc.InternalAddresses.SelectMany(add => add.Transactions).Any()).ToList();
            if (!unusedAccounts.Any())
                return null;

            // gets the unused account with the lowest index
            int index = unusedAccounts.Min(a => a.Index);
            return unusedAccounts.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns>The HD account specified by the parameter or <c>null</c> if the account does not exist.</returns>
        public HdAccount GetAccountByName(string accountName)
        {
            return this.Accounts?.SingleOrDefault(a => a.Name == accountName);
        }

        /// <summary>
        /// Adds an account to the current account root using encrypted seed and password.
        /// </summary>
        /// <remarks>The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains transactions.
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/></remarks>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="accountIndex">The index at which an account will be created. If left null, a new account will be created after the last used one.</param>
        /// <param name="accountName">The name of the account to be created. If left null, an account will be created according to the <see cref="AccountNamePattern"/>.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network, DateTimeOffset accountCreationTime, int? accountIndex = null, string accountName = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            ICollection<HdAccount> hdAccounts = this.Accounts;

            // If an account needs to be created at a specific index or with a specific name, make sure it doesn't already exist.
            if (hdAccounts.Any(a => a.Index == accountIndex || a.Name == accountName))
            {
                throw new Exception($"An account at index {accountIndex} or with name {accountName} already exists.");
            }

            if (accountIndex == null)
            {
                if (hdAccounts.Any())
                {
                    // Hide account indexes used for cold staking from the "Max" calculation.
                    accountIndex = hdAccounts.Where(a => a.Index < 100_000_000).Max(a => a.Index) + 1;
                }
                else
                {
                    accountIndex = 0;
                }
            }

            HdAccount newAccount = this.CreateAccount(password, encryptedSeed, chainCode, network, accountCreationTime, accountIndex.Value, accountName);

            hdAccounts.Add(newAccount);
            this.Accounts = hdAccounts;

            return newAccount;
        }

        /// <summary>
        /// Create an account for a specific account index and account name pattern.
        /// </summary>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="newAccountIndex">The optional account index to use.</param>
        /// <param name="newAccountName">The optional account name to use.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount CreateAccount(string password, string encryptedSeed, byte[] chainCode,
            Network network, DateTimeOffset accountCreationTime,
            int newAccountIndex, string newAccountName = null)
        {
            if (string.IsNullOrEmpty(newAccountName))
            {
                newAccountName = string.Format("account {0}", newAccountIndex);
            }

            // Get the extended pub key used to generate addresses for this account.
            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, newAccountIndex);
            Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, network);
            ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);

            return new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = newAccountName,
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };
        }

        /// <inheritdoc cref="AddNewAccount(string, string, byte[], Network, DateTimeOffset)"/>
        /// <summary>
        /// Adds an account to the current account root using extended public key and account index.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key for the account.</param>
        /// <param name="accountIndex">The zero-based account index.</param>
        public HdAccount AddNewAccount(ExtPubKey accountExtPubKey, int accountIndex, Network network, DateTimeOffset accountCreationTime)
        {
            ICollection<HdAccount> hdAccounts = this.Accounts.ToList();

            if (hdAccounts.Any(a => a.Index == accountIndex))
            {
                throw new Exception("There is already an account in this wallet with index: " + accountIndex);
            }

            if (hdAccounts.Any(x => x.ExtendedPubKey == accountExtPubKey.ToString(network)))
            {
                throw new Exception("There is already an account in this wallet with this xpubkey: " +
                                            accountExtPubKey.ToString(network));
            }

            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, accountIndex);

            var newAccount = new HdAccount
            {
                Index = accountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {accountIndex}",
                HdPath = accountHdPath,
                CreationTime = accountCreationTime
            };

            hdAccounts.Add(newAccount);
            this.Accounts = hdAccounts;

            return newAccount;
        }
    }
    /// <summary>
    /// An HD account's details.
    /// </summary>
    public class HdAccount
    {
        public HdAccount()
        {
            this.ExternalAddresses = new List<HdAddress>();
            this.InternalAddresses = new List<HdAddress>();
        }

        /// <summary>
        /// The index of the account.
        /// </summary>
        /// <remarks>
        /// According to BIP44, an account at index (i) can only be created when the account
        /// at index (i - 1) contains transactions.
        /// </remarks>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The name of this account.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// A path to the account as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// An extended pub key used to generate addresses.
        /// </summary>
        [JsonProperty(PropertyName = "extPubKey")]
        public string ExtendedPubKey { get; set; }

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
        public ICollection<HdAddress> ExternalAddresses { get; set; }

        /// <summary>
        /// The list of internal addresses, typically used to receive change.
        /// </summary>
        [JsonProperty(PropertyName = "internalAddresses")]
        public ICollection<HdAddress> InternalAddresses { get; set; }

        /// <summary>
        /// Gets the type of coin this account is for.
        /// </summary>
        /// <returns>A <see cref="CoinType"/>.</returns>
        public CoinType GetCoinType()
        {
            return (CoinType)HdOperations.GetCoinType(this.HdPath);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedReceivingAddress()
        {
            return this.GetFirstUnusedAddress(false);
        }

        /// <summary>
        /// Gets the first change address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        public HdAddress GetFirstUnusedChangeAddress()
        {
            return this.GetFirstUnusedAddress(true);
        }

        /// <summary>
        /// Gets the first receiving address that contains no transaction.
        /// </summary>
        /// <returns>An unused address</returns>
        private HdAddress GetFirstUnusedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            List<HdAddress> unusedAddresses = addresses.Where(acc => !acc.Transactions.Any()).ToList();
            if (!unusedAddresses.Any())
            {
                return null;
            }

            // gets the unused address with the lowest index
            int index = unusedAddresses.Min(a => a.Index);
            return unusedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the last address that contains transactions.
        /// </summary>
        /// <param name="isChange">Whether the address is a change (internal) address or receiving (external) address.</param>
        /// <returns></returns>
        public HdAddress GetLastUsedAddress(bool isChange)
        {
            IEnumerable<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;
            if (addresses == null)
                return null;

            List<HdAddress> usedAddresses = addresses.Where(acc => acc.Transactions.Any()).ToList();
            if (!usedAddresses.Any())
            {
                return null;
            }

            // gets the used address with the highest index
            int index = usedAddresses.Max(a => a.Index);
            return usedAddresses.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets a collection of transactions by id.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetTransactionsById(uint256 id)
        {
            Guard.NotNull(id, nameof(id));

            IEnumerable<HdAddress> addresses = this.GetCombinedAddresses();
            return addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.Id == id));
        }

        /// <summary>
        /// Get the accounts total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money ConfirmedAmount, Money UnConfirmedAmount) GetBalances()
        {
            List<TransactionData> allTransactions = this.ExternalAddresses.SelectMany(a => a.Transactions)
                .Concat(this.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

            long confirmed = allTransactions.Sum(t => t.GetUnspentAmount(true));
            long total = allTransactions.Sum(t => t.GetUnspentAmount(false));

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
        public IEnumerable<HdAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate)
        {
            Guard.NotNull(predicate, nameof(predicate));

            IEnumerable<HdAddress> addresses = this.GetCombinedAddresses();
            return addresses.Where(t => t.Transactions != null).Where(a => a.Transactions.Any(predicate));
        }

        /// <summary>
        /// Return both the external and internal (change) address from an account.
        /// </summary>
        /// <returns>All addresses that belong to this account.</returns>
        public IEnumerable<HdAddress> GetCombinedAddresses()
        {
            IEnumerable<HdAddress> addresses = new List<HdAddress>();
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
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <param name="network">The network these addresses will be for.</param>
        /// <param name="addressesQuantity">The number of addresses to create.</param>
        /// <param name="isChange">Whether the addresses added are change (internal) addresses or receiving (external) addresses.</param>
        /// <returns>The created addresses.</returns>
        public IEnumerable<HdAddress> CreateAddresses(Network network, int addressesQuantity, bool isChange = false)
        {
            ICollection<HdAddress> addresses = isChange ? this.InternalAddresses : this.ExternalAddresses;

            // Get the index of the last address.
            int firstNewAddressIndex = 0;
            if (addresses.Any())
            {
                firstNewAddressIndex = addresses.Max(add => add.Index) + 1;
            }

            var addressesCreated = new List<HdAddress>();
            for (int i = firstNewAddressIndex; i < firstNewAddressIndex + addressesQuantity; i++)
            {
                // Retrieve the pubkey associated with the private key of this address index.
                PubKey pubkey = HdOperations.GeneratePublicKey(this.ExtendedPubKey, i, isChange);

                // Generate the P2PKH address corresponding to the pubkey.
                BitcoinPubKeyAddress address = pubkey.GetAddress(network);

                // Add the new address details to the list of addresses.
                var newAddress = new HdAddress
                {
                    Index = i,
                    HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, isChange, i),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey.ScriptPubKey,
                    Address = address.ToString(),
                    Transactions = new List<TransactionData>()
                };

                addresses.Add(newAddress);
                addressesCreated.Add(newAddress);
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
        /// <param name="coinbaseMaturity">The coinbase maturity after which a coinstake transaction is spendable.</param>
        /// <param name="confirmations">The minimum number of confirmations required for transactions to be considered.</param>
        /// <returns>A collection of spendable outputs that belong to the given account.</returns>
        /// <remarks>Note that coinbase and coinstake transaction outputs also have to mature with a sufficient number of confirmations before
        /// they are considered spendable. This is independent of the confirmations parameter.</remarks>
        public IEnumerable<UnspentOutputReference> GetSpendableTransactions(int currentChainHeight, long coinbaseMaturity, int confirmations = 0)
        {
            // This will take all the spendable coins that belong to the account and keep the reference to the HdAddress and HdAccount.
            // This is useful so later the private key can be calculated just from a given UTXO.
            foreach (HdAddress address in this.GetCombinedAddresses())
            {
                // A block that is at the tip has 1 confirmation.
                // When calculating the confirmations the tip must be advanced by one.

                int countFrom = currentChainHeight + 1;
                foreach (TransactionData transactionData in address.UnspentTransactions())
                {
                    int? confirmationCount = 0;
                    if (transactionData.BlockHeight != null)
                        confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

                    if (confirmationCount < confirmations)
                        continue;

                    bool isCoinBase = transactionData.IsCoinBase ?? false;
                    bool isCoinStake = transactionData.IsCoinStake ?? false;

                    // This output can unconditionally be included in the results.
                    // Or this output is a CoinBase or CoinStake and has reached maturity.
                    if ((!isCoinBase && !isCoinStake) || (confirmationCount > coinbaseMaturity))
                    {
                        yield return new UnspentOutputReference
                        {
                            Account = this,
                            Address = address,
                            Transaction = transactionData,
                            Confirmations = confirmationCount.Value
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// An HD address.
    /// </summary>
    public class HdAddress
    {
        public HdAddress()
        {
            this.Transactions = new List<TransactionData>();
        }

        /// <summary>
        /// The index of the address.
        /// </summary>
        [JsonProperty(PropertyName = "index")]
        public int Index { get; set; }

        /// <summary>
        /// The P2PKH (pay-to-pubkey-hash) script pub key for this address.
        /// </summary>
        /// <remarks>The script is of the format OP_DUP OP_HASH160 {pubkeyhash} OP_EQUALVERIFY OP_CHECKSIG</remarks>
        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        /// <summary>
        /// The P2PK (pay-to-pubkey) script pub key corresponding to the private key of this address.
        /// </summary>
        /// <remarks>This is typically only used for mining, as the valid script types for mining are constrained.
        /// Block explorers often depict the P2PKH address as the 'address' of a P2PK scriptPubKey, which is not
        /// actually correct. A P2PK scriptPubKey does not have a defined address format.
        /// 
        /// The script itself is of the format: {pubkey} OP_CHECKSIG</remarks>
        [JsonProperty(PropertyName = "pubkey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script Pubkey { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public ICollection<TransactionData> Transactions { get; set; }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        [NoTrace]
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(this.HdPath);
        }

        /// <summary>
        /// List all spendable transactions in an address.
        /// </summary>
        /// <returns>List of spendable transactions.</returns>
        [NoTrace]
        public IEnumerable<TransactionData> UnspentTransactions()
        {
            if (this.Transactions == null)
            {
                return new List<TransactionData>();
            }

            return this.Transactions.Where(t => !t.IsSpent());
        }

        /// <summary>
        /// Get the address total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money confirmedAmount, Money unConfirmedAmount) GetBalances()
        {
            List<TransactionData> allTransactions = this.Transactions.ToList();

            long confirmed = allTransactions.Sum(t => t.GetUnspentAmount(true));
            long total = allTransactions.Sum(t => t.GetUnspentAmount(false));

            return (confirmed, total - confirmed);
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
        /// A value indicating whether this is a coinbase transaction or not.
        /// </summary>
        [JsonProperty(PropertyName = "isCoinBase", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsCoinBase { get; set; }

        /// <summary>
        /// A value indicating whether this is a coinstake transaction or not.
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
        /// The index of this transaction in the block in which it is contained.
        /// </summary>
        [JsonProperty(PropertyName = "blockIndex", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockIndex { get; set; }

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
        /// The details of the transaction in which the output referenced in this transaction is spent.
        /// </summary>
        [JsonProperty(PropertyName = "spendingDetails", NullValueHandling = NullValueHandling.Ignore)]
        public SpendingDetails SpendingDetails { get; set; }

        /// <summary>
        /// Determines whether this transaction is confirmed.
        /// </summary>
        [NoTrace]
        public bool IsConfirmed()
        {
            return this.BlockHeight != null;
        }

        /// <summary>
        /// Indicates whether an output has been spent.
        /// </summary>
        /// <returns>A value indicating whether an output has been spent.</returns>
        [NoTrace]
        public bool IsSpent()
        {
            return this.SpendingDetails != null;
        }

        /// <summary>
        /// Checks if the output is not spent, with the option to choose whether only confirmed ones are considered. 
        /// </summary>
        /// <param name="confirmedOnly">A value indicating whether we only want confirmed amount.</param>
        /// <returns>The total amount that has not been spent.</returns>
        [NoTrace]
        public Money GetUnspentAmount(bool confirmedOnly)
        {
            // The spendable balance is 0 if the output is spent or it needs to be confirmed to be considered.
            if (this.IsSpent() || (confirmedOnly && !this.IsConfirmed()))
            {
                return Money.Zero;
            }

            return this.Amount;
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
        /// The Base58 representation of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "destinationAddress")]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The index of the output of the destination address.
        /// </summary>
        [JsonProperty(PropertyName = "outputIndex", NullValueHandling = NullValueHandling.Ignore)]
        public int? OutputIndex { get; set; }

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
        /// The index of this transaction in the block in which it is contained.
        /// </summary>
        [JsonProperty(PropertyName = "blockIndex", NullValueHandling = NullValueHandling.Ignore)]
        public int? BlockIndex { get; set; }

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
        /// Determines whether this transaction being spent is confirmed.
        /// </summary>
        public bool IsSpentConfirmed()
        {
            return this.BlockHeight != null;
        }
    }

    /// <summary>
    /// Represents an UTXO that keeps a reference to <see cref="HdAddress"/> and <see cref="HdAccount"/>.
    /// </summary>
    /// <remarks>
    /// This is useful when an UTXO needs access to its HD properties like the HD path when reconstructing a private key.
    /// </remarks>
    public class UnspentOutputReference
    {
        /// <summary>
        /// The account associated with this UTXO
        /// </summary>
        public HdAccount Account { get; set; }

        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public HdAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }

        /// <summary>
        /// Number of confirmations for this UTXO.
        /// </summary>
        public int Confirmations { get; set; }

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
    /// The type of coin, as specified in BIP44.
    /// </summary>
    /// <remarks>For more, see https://github.com/satoshilabs/slips/blob/master/slip-0044.md</remarks>
    public enum CoinType
    {
        /// <summary>
        /// Bitcoin
        /// </summary>
        Bitcoin = 0,

        /// <summary>
        /// Testnet (all coins)
        /// </summary>
        Testnet = 1,

        /// <summary>
        /// Stratis
        /// </summary>
        Stratis = 105
    }

    /// <summary>
    /// Class providing helper methods for working with Hierarchical Deterministic (HD) wallets.
    /// </summary>
    /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki" />
    public class HdOperations
    {
        /// <summary>
        /// Generates an HD public key derived from an extended public key.
        /// </summary>
        /// <param name="accountExtPubKey">The extended public key used to generate child keys.</param>
        /// <param name="index">The index of the child key to generate.</param>
        /// <param name="isChange">A value indicating whether the public key to generate corresponds to a change address.</param>
        /// <returns>
        /// An HD public key derived from an extended public key.
        /// </returns>
        public static PubKey GeneratePublicKey(string accountExtPubKey, int index, bool isChange)
        {
            Guard.NotEmpty(accountExtPubKey, nameof(accountExtPubKey));

            int change = isChange ? 1 : 0;
            var keyPath = new KeyPath($"{change}/{index}");
            // TODO: Should probably explicitly be passing the network into Parse
            ExtPubKey extPubKey = ExtPubKey.Parse(accountExtPubKey).Derive(keyPath);
            return extPubKey.PubKey;
        }

        /// <summary>
        /// Gets the extended private key for an account.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended private key.</param>
        /// <param name="chainCode">The chain code used in creating the extended private key.</param>
        /// <param name="hdPath">The HD path of the account for which to get the extended private key.</param>
        /// <param name="network">The network for which to generate this extended private key.</param>
        /// <returns></returns>
        [NoTrace]
        public static ISecret GetExtendedPrivateKey(Key privateKey, byte[] chainCode, string hdPath, Network network)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));
            Guard.NotEmpty(hdPath, nameof(hdPath));
            Guard.NotNull(network, nameof(network));

            // Get the extended key.
            var seedExtKey = new ExtKey(privateKey, chainCode);
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(hdPath));
            BitcoinExtKey addressPrivateKey = addressExtKey.GetWif(network);
            return addressPrivateKey;
        }

        /// <summary>
        /// Gets the extended public key for an account.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended public key.</param>
        /// <param name="chainCode">The chain code used in creating the extended public key.</param>
        /// <param name="coinType">Type of the coin of the account for which to generate an extended public key.</param>
        /// <param name="accountIndex">Index of the account for which to generate an extended public key.</param>
        /// <returns>The extended public key for an account, used to derive child keys.</returns>
        public static ExtPubKey GetExtendedPublicKey(Key privateKey, byte[] chainCode, int coinType, int accountIndex)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));

            string accountHdPath = GetAccountHdPath(coinType, accountIndex);
            return GetExtendedPublicKey(privateKey, chainCode, accountHdPath);
        }

        /// <summary>
        /// Gets the extended public key corresponding to an HD path.
        /// </summary>
        /// <param name="privateKey">The private key from which to generate the extended public key.</param>
        /// <param name="chainCode">The chain code used in creating the extended public key.</param>
        /// <param name="hdPath">The HD path for which to get the extended public key.</param>
        /// <returns>The extended public key, used to derive child keys.</returns>
        public static ExtPubKey GetExtendedPublicKey(Key privateKey, byte[] chainCode, string hdPath)
        {
            Guard.NotNull(privateKey, nameof(privateKey));
            Guard.NotNull(chainCode, nameof(chainCode));
            Guard.NotEmpty(hdPath, nameof(hdPath));

            // get extended private key
            var seedExtKey = new ExtKey(privateKey, chainCode);
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(hdPath));
            ExtPubKey extPubKey = addressExtKey.Neuter();
            return extPubKey;
        }

        /// <summary>
        /// Gets the HD path of an account.
        /// </summary>
        /// <param name="coinType">Type of the coin this account is in.</param>
        /// <param name="accountIndex">Index of the account.</param>
        /// <returns>The HD path of an account.</returns>
        public static string GetAccountHdPath(int coinType, int accountIndex)
        {
            return $"m/44'/{coinType}'/{accountIndex}'";
        }

        /// <summary>
        /// Gets the extended key generated by this mnemonic and passphrase.
        /// </summary>
        /// <param name="mnemonic">The mnemonic used to generate the key.</param>
        /// <param name="passphrase">The passphrase used in generating the key.</param>
        /// <returns>The extended key generated by this mnemonic and passphrase.</returns>
        /// <remarks>This key is sometimes referred to as the 'root seed' or the 'master key'.</remarks>
        public static ExtKey GetExtendedKey(string mnemonic, string passphrase = null)
        {
            Guard.NotEmpty(mnemonic, nameof(mnemonic));

            return GetExtendedKey(new Mnemonic(mnemonic), passphrase);
        }

        /// <summary>
        /// Gets the extended key generated by this mnemonic and passphrase.
        /// </summary>
        /// <param name="mnemonic">The mnemonic used to generate the key.</param>
        /// <param name="passphrase">The passphrase used in generating the key.</param>
        /// <returns>The extended key generated by this mnemonic and passphrase.</returns>
        /// <remarks>This key is sometimes referred to as the 'root seed' or the 'master key'.</remarks>
        public static ExtKey GetExtendedKey(Mnemonic mnemonic, string passphrase = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));

            return mnemonic.DeriveExtKey(passphrase);
        }

        /// <summary>
        /// Creates an address' HD path, according to BIP 44.
        /// </summary>
        /// <param name="coinType">Type of coin in the HD path.</param>
        /// <param name="accountIndex">Index of the account in the HD path.</param>
        /// <param name="isChange">A value indicating whether the HD path to generate corresponds to a change address.</param>
        /// <param name="addressIndex">Index of the address in the HD path.</param>
        /// <returns>The HD path.</returns>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static string CreateHdPath(int coinType, int accountIndex, bool isChange, int addressIndex)
        {
            int change = isChange ? 1 : 0;
            return $"m/44'/{coinType}'/{accountIndex}'/{change}/{addressIndex}";
        }

        /// <summary>
        /// Gets the type of coin this HD path is for.
        /// </summary>
        /// <param name="hdPath">The HD path.</param>
        /// <returns>The type of coin. <seealso cref="https://github.com/satoshilabs/slips/blob/master/slip-0044.md"/>.</returns>
        /// <exception cref="FormatException">An exception is thrown if the HD path is not well-formed.</exception>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        public static int GetCoinType(string hdPath)
        {
            Guard.NotEmpty(hdPath, nameof(hdPath));

            string[] pathElements = hdPath.Split('/');
            if (pathElements.Length < 3)
                throw new FormatException($"Could not parse CoinType from HdPath {hdPath}.");

            int coinType = 0;
            if (int.TryParse(pathElements[2].Replace("'", string.Empty), out coinType))
            {
                return coinType;
            }

            throw new FormatException($"Could not parse CoinType from HdPath {hdPath}.");
        }

        /// <summary>
        /// Determines whether the HD path corresponds to a change address.
        /// </summary>
        /// <param name="hdPath">The HD path.</param>
        /// <returns>A value indicating if the HD path corresponds to a change address.</returns>
        /// <exception cref="FormatException">An exception is thrown if the HD path is not well-formed.</exception>
        /// <remarks>Refer to <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki#path-levels"/> for the format of the HD path.</remarks>
        [NoTrace]
        public static bool IsChangeAddress(string hdPath)
        {
            Guard.NotEmpty(hdPath, nameof(hdPath));

            string[] hdPathParts = hdPath.Split('/');
            if (hdPathParts.Length < 5)
                throw new FormatException($"Could not parse value from HdPath {hdPath}.");

            int result = 0;
            if (int.TryParse(hdPathParts[4], out result))
            {
                return result == 1;
            }

            return false;
        }

        /// <summary>
        /// Decrypts the encrypted private key (seed).
        /// </summary>
        /// <param name="encryptedSeed">The encrypted seed to decrypt.</param>
        /// <param name="password">The password used to decrypt the encrypted seed.</param>
        /// <param name="network">The network this seed applies to.</param>
        /// <returns>The decrypted private key.</returns>
        [NoTrace]
        public static Key DecryptSeed(string encryptedSeed, string password, Network network)
        {
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(network, nameof(network));

            return Key.Parse(encryptedSeed, password, network);
        }
    }

    public class WalletAccountReference
    {
        public WalletAccountReference()
        {
        }

        public WalletAccountReference(string walletName, string accountName)
        {
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(accountName, nameof(accountName));

            this.WalletName = walletName;
            this.AccountName = accountName;
        }

        public string WalletName { get; set; }

        public string AccountName { get; set; }

        public override bool Equals(object obj)
        {
            var item = obj as WalletAccountReference;
            if (item == null)
                return false;
            return this.GetId().Equals(item.GetId());
        }

        public static bool operator ==(WalletAccountReference a, WalletAccountReference b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return a.GetId().Equals(b.GetId());
        }

        public static bool operator !=(WalletAccountReference a, WalletAccountReference b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.GetId().GetHashCode();
        }

        internal Tuple<string, string> GetId()
        {
            return Tuple.Create(this.WalletName, this.AccountName);
        }

        public override string ToString()
        {
            return $"{this.WalletName}:{this.AccountName}";
        }
    }
}
