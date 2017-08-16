using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.JsonConverters;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A wallet
    /// </summary>
    public class Wallet
    {
        public Wallet()
        {
            this.AccountsRoot = new List<AccountRoot>();
        }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        [JsonProperty(PropertyName = "encryptedSeed")]
        public string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code. 
        /// </summary>
        [JsonProperty(PropertyName = "chainCode")]
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] ChainCode { get; set; }

        /// <summary>
        /// Gets or sets the merkle path.
        /// </summary>
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
        public ICollection<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The network this wallet is for.
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

        /// <summary>
        /// Gets the type of the accounts by coin.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<HdAccount> GetAccountsByCoinType(CoinType coinType)
        {
            if (this.AccountsRoot == null)
                return new List<HdAccount>();

            return this.AccountsRoot.Where(a => a.CoinType == coinType).SelectMany(a => a.Accounts);
        }

        /// <summary>
        /// Gets all the transactions by coin type.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<TransactionData> GetAllTransactionsByCoinType(CoinType coinType)
        {            
            var accounts = this.GetAccountsByCoinType(coinType).ToList();

            List<TransactionData> result = new List<TransactionData>();
            foreach (var address in accounts.SelectMany(a => a.ExternalAddresses).Concat(accounts.SelectMany(a => a.InternalAddresses)))
            {
                result.AddRange(address.Transactions);
            }
            return result;
        }

        /// <summary>
        /// Gets all the pub keys contained in this wallet.
        /// </summary>
        /// <param name="coinType">Type of the coin.</param>
        /// <returns></returns>
        public IEnumerable<Script> GetAllPubKeysByCoinType(CoinType coinType)
        {
            var accounts = this.GetAccountsByCoinType(coinType).ToList();
            foreach (var address in accounts.SelectMany(a => a.ExternalAddresses).Concat(accounts.SelectMany(a => a.InternalAddresses)))
            {
                yield return address.ScriptPubKey;
            }
        }

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.        
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="password">The password used to decrypt the wallet's <see cref="EncryptedSeed"/>.</param>
        /// <param name="coinType">The type of coin this account is for.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, CoinType coinType)
        {
            Guard.NotEmpty(password, nameof(password));

            var accountRoot = this.AccountsRoot.Single(a => a.CoinType == coinType);
            return accountRoot.AddNewAccount(password, this.EncryptedSeed, this.ChainCode, this.Network);
        }
    }

    /// <summary>
    /// The root for the accounts for any type of coins.
    /// </summary>
    public class AccountRoot
    {
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

            var unusedAccounts = this.Accounts.Where(acc => !acc.ExternalAddresses.Any() && !acc.InternalAddresses.Any()).ToList();
            if (!unusedAccounts.Any())            
                return null;            

            // gets the unused account with the lowest index
            var index = unusedAccounts.Min(a => a.Index);
            return unusedAccounts.Single(a => a.Index == index);
        }

        /// <summary>
        /// Gets the account matching the name passed as a parameter.
        /// </summary>
        /// <param name="accountName">The name of the account to get.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public HdAccount GetAccountByName(string accountName)
        {
            if (this.Accounts == null)
                throw new Exception($"No account with the name {accountName} could be found.");

            // get the account
            HdAccount account = this.Accounts.SingleOrDefault(a => a.Name == accountName);
            if (account == null)            
                throw new Exception($"No account with the name {accountName} could be found.");
            
            return account;
        }

        /// <summary>
        /// Adds an account to the current account root.
        /// </summary>
        /// <remarks>The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains transactions.
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/></remarks>
        /// <param name="password">The password used to decrypt the wallet's encrypted seed.</param>
        /// <param name="encryptedSeed">The encrypted private key for this wallet.</param>
        /// <param name="chainCode">The chain code for this wallet.</param>
        /// <param name="network">The network for which this account will be created.</param>
        /// <returns>A new hd account.</returns>
        public HdAccount AddNewAccount(string password, string encryptedSeed, byte[] chainCode, Network network)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(encryptedSeed, nameof(encryptedSeed));
            Guard.NotNull(chainCode, nameof(chainCode));

            // Get the current collection of accounts.
            var accounts = this.Accounts.ToList();

            int newAccountIndex = 0;
            if (accounts.Any())
            {
                newAccountIndex = accounts.Max(a => a.Index) + 1;
            }

            // Get the extended pub key used to generate addresses for this account.
            string accountHdPath = HdOperations.GetAccountHdPath((int)this.CoinType, newAccountIndex);
            Key privateKey = HdOperations.DecryptSeed(encryptedSeed, password, network);
            ExtPubKey accountExtPubKey = HdOperations.GetExtendedPublicKey(privateKey, chainCode, accountHdPath);
            
            var newAccount = new HdAccount
            {
                Index = newAccountIndex,
                ExtendedPubKey = accountExtPubKey.ToString(network),
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Name = $"account {newAccountIndex}",
                HdPath = accountHdPath,
                CreationTime = DateTimeOffset.Now
            };

            accounts.Add(newAccount);
            this.Accounts = accounts;

            return newAccount;
        }
    }
    
    /// <summary>
    /// An Hd account's details.
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

            var unusedAddresses = addresses.Where(acc => !acc.Transactions.Any()).ToList();
            if (!unusedAddresses.Any())
            {
                return null;
            }

            // gets the unused address with the lowest index
            var index = unusedAddresses.Min(a => a.Index);
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

            var usedAddresses = addresses.Where(acc => acc.Transactions.Any()).ToList();
            if (!usedAddresses.Any())
            {
                return null;
            }

            // gets the used address with the highest index
            var index = usedAddresses.Max(a => a.Index);
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
        public (Money ConfirmedAmount, Money UnConfirmedAmount) GetSpendableAmount()
        {
            var allTransactions = this.ExternalAddresses.SelectMany(a => a.Transactions)
                .Concat(this.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

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
        public IEnumerable<HdAddress> FindAddressesForTransaction(Func<TransactionData, bool> predicate)
        {
            Guard.NotNull(predicate, nameof(predicate));

            var addresses = this.GetCombinedAddresses();
            return addresses.Where(t=> t.Transactions != null).Where(a => a.Transactions.Any(predicate));
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
    }

    /// <summary>
    /// An Hd address.
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
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath { get; set; }

        /// <summary>
        /// A list detailing which blocks have been scanned for this address.
        /// </summary>
        [JsonIgnore]
        public SortedList<int, int> BlocksScanned { get; set; }

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
        public bool IsChangeAddress()
        {
            return HdOperations.IsChangeAddress(this.HdPath);
        }

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
        /// A list of payments made out in this transaction.
        /// </summary>
        [JsonProperty(PropertyName = "payments", NullValueHandling = NullValueHandling.Ignore)]
        public ICollection<PaymentDetails> Payments { get; set; }

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
            return this.SpendingDetails == null;
        }

        public Money SpendableAmount(bool confirmedOnly)
        {
            // this method only returns a UTXO that has no spending output.
            // if a spending output exists (even if its not confirmed) this 
            // will return as zero balance.
            
            if (this.IsSpendable())
            {
                // if the 'confirmedOnly' flag is set check 
                // that the UTXO is confirmed.
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
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// Determines whether this transaction being spent is confirmed.
        /// </summary>    
        public bool IsSpentConfirmed()
        {
            return this.BlockHeight != null;
        }
    }
}