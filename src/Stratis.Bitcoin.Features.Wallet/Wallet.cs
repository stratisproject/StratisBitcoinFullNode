using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Wallet
{
    /// <summary>
    /// A wallet.
    /// </summary>
    public class Wallet
    {
        /// <summary>Default pattern for accounts in the wallet. The first account will be called 'account 0', then 'account 1' and so on.</summary>
        public const string AccountNamePattern = "account {0}";

        /// <summary>Account numbers greater or equal to this number are reserved for special purpose account indexes.</summary>
        public const int SpecialPurposeAccountIndexesStart = 100_000_000;

        /// <summary>Filter for identifying normal wallet accounts.</summary>
        public static Func<HdAccount, bool> NormalAccounts = a => a.Index < SpecialPurposeAccountIndexesStart;

        /// <summary>Filter for all wallet accounts.</summary>
        public static Func<HdAccount, bool> AllAccounts = a => true;

        [JsonIgnore]
        public IWalletRepository WalletRepository { get; private set;}

        [JsonIgnore]
        internal IWalletManager WalletManager { get; set; }

        [JsonConstructor]
        public Wallet(ICollection<AccountRoot> accountsRoot)
        {
            foreach (AccountRoot accountRoot in accountsRoot)
                accountRoot.Wallet = this;

            this.AccountsRoot = accountsRoot;
            this.BlockLocator = new List<uint256>();
        }

        public Wallet()
        {
            this.AccountsRoot = new List<AccountRoot>();
            this.BlockLocator = new List<uint256>();
        }

        public Wallet(Network network)
            : this()
        {
            this.Network = network;
            this.AccountsRoot.Add(new AccountRoot(this) {
                CoinType = (CoinType)network.Consensus.CoinType
            });
        }

        public Wallet(IWalletRepository walletRepository)
            : this()
        {
            this.WalletRepository = walletRepository;
            this.Network = walletRepository?.Network;
        }

        /// <summary>
        /// Initializes a new instance of the wallet.
        /// </summary>
        public Wallet(string name, string encryptedSeed = null, byte[] chainCode = null, DateTimeOffset? creationTime = null, ChainedHeader lastBlockSynced = null, IWalletRepository walletRepository = null)
            : this(walletRepository)
        {
            this.Name = name;
            this.EncryptedSeed = encryptedSeed;
            this.ChainCode = chainCode;

            if (walletRepository != null)
            {
                Wallet repoWallet;

                HashHeightPair lastBlock = (lastBlockSynced == null) ? null : new HashHeightPair(lastBlockSynced);
                BlockLocator blockLocator = lastBlockSynced?.GetLocator();
                uint? unixCreationTime = (creationTime == null || lastBlockSynced?.Header == null) ? (uint?)null : (lastBlockSynced.Header.Time + 1);
                if (lastBlockSynced != null && unixCreationTime == null)
                    unixCreationTime = lastBlockSynced.Header.Time + 1;

                repoWallet = walletRepository?.CreateWallet(name, encryptedSeed, chainCode, lastBlock, blockLocator, unixCreationTime);

                this.BlockLocator = repoWallet.BlockLocator;
                this.CreationTime = repoWallet.CreationTime;
                this.AccountsRoot = repoWallet.AccountsRoot;
                this.Network = repoWallet.Network;
            }
        }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Flag indicating if it is a watch only wallet.
        /// </summary>
        [JsonProperty(PropertyName = "isExtPubKeyWallet")]
        public bool IsExtPubKeyWallet => string.IsNullOrEmpty(this.EncryptedSeed);

        /// <summary>
        /// The seed for this wallet, password encrypted.
        /// </summary>
        [JsonProperty(PropertyName = "encryptedSeed", NullValueHandling = NullValueHandling.Ignore)]
        public string EncryptedSeed { get; set; }

        /// <summary>
        /// The chain code.
        /// </summary>
        [JsonProperty(PropertyName = "chainCode", NullValueHandling = NullValueHandling.Ignore)]
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
        /// Gets the accounts in the wallet.
        /// </summary>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>The accounts in the wallet.</returns>
        public IEnumerable<HdAccount> GetAccounts(Func<HdAccount, bool> accountFilter = null)
        {
            if (this.AccountsRoot.Any())
            {
                foreach (HdAccount account in (this.AccountsRoot.First().Accounts).GetAccounts(accountFilter))
                    yield return account;
            }
        }

        /// <summary>
        /// Gets an account from the wallet's accounts.
        /// </summary>
        /// <param name="accountName">The name of the account to retrieve.</param>
        /// <returns>The requested account or <c>null</c> if the account does not exist.</returns>
        public HdAccount GetAccount(string accountName)
        {
            return GetAccounts(a => a.Name == accountName).FirstOrDefault();
        }

        /// <summary>
        /// Gets all the transactions in the wallet.
        /// </summary>
        /// <returns>A list of all the transactions in the wallet.</returns>
        public IEnumerable<TransactionData> GetAllTransactions(DateTimeOffset? transactionTime = null, uint256 spendingTransactionId = null, Func<HdAccount, bool> accountFilter = null)
        {
            if (accountFilter == null)
                accountFilter = NormalAccounts;

            /*
            if (this.WalletRepository != null)
            {
                if (transactionTime != null && spendingTransactionId != null)
                {
                    foreach (TransactionData txData in this.WalletRepository.GetTransactionInputs(this.Name, null, transactionTime, spendingTransactionId, includePayments: true))
                        yield return txData;
                }
                else
                {
                    foreach (TransactionData txData in this.WalletRepository.GetAllTransactions(this.WalletRepository.GetAddressIdentifier(this.Name), includePayments: true))
                        yield return txData;
                }
            }
            else */
            {
                List<HdAccount> accounts = this.GetAccounts(accountFilter).ToList();

                foreach (TransactionData txData in accounts.SelectMany(x => x.ExternalAddresses)
                    .SelectMany(x => x.Transactions)
                    .Where(td => spendingTransactionId == null || td.SpendingDetails?.TransactionId == spendingTransactionId))
                {
                    yield return txData;
                }

                foreach (TransactionData txData in accounts.SelectMany(x => x.InternalAddresses)
                    .SelectMany(x => x.Transactions)
                    .Where(td => spendingTransactionId == null || td.SpendingDetails?.TransactionId == spendingTransactionId))
                {
                    yield return txData;
                }
            }
        }

        /// <summary>
        /// Gets all the addresses contained in this wallet.
        /// </summary>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>A list of all the addresses contained in this wallet.</returns>
        public IEnumerable<HdAddress> GetAllAddresses(Func<HdAccount, bool> accountFilter = null)
        {
            IEnumerable<HdAccount> accounts = this.GetAccounts(accountFilter);

            var allAddresses = new List<HdAddress>();
            foreach (HdAccount account in accounts)
            {
                allAddresses.AddRange(account.GetCombinedAddresses());
            }
            return allAddresses;
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
        /// <param name="accountIndex">Zero-based index of the account to add.</param>
        /// <param name="accountName">The name of the account.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="addressCounts">The number of external and change addresses to create.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(string password, int? accountIndex = null, string accountName = null, DateTimeOffset? accountCreationTime = null, (int external, int change)? addressCounts = null)
        {
            Guard.NotEmpty(password, nameof(password));

            // Get the extended pub key used to generate addresses for this account.
            Key privateKey = Key.Parse(this.EncryptedSeed, password, this.Network);
            var seedExtKey = new ExtKey(privateKey, this.ChainCode);
            accountIndex = accountIndex ?? this.NextAccountIndex();
            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath($"m/44'/{this.Network.Consensus.CoinType}'/{accountIndex}'"));
            ExtPubKey extPubKey = addressExtKey.Neuter();

            return AddNewAccount(extPubKey, accountIndex, accountName, accountCreationTime, addressCounts);
        }

        private int NextAccountIndex()
        {
            // TODO: IEnumerable<HdAccount> accounts = this.WalletManager.GetAccounts(this.Name);
            //       Only WalletManager shiuld reference repository directly?
            IEnumerable<HdAccount> accounts = (this.WalletRepository == null) ? this.AccountsRoot.First().Accounts : this.WalletRepository.GetAccounts(this);

            return accounts.Concat(new[] { new HdAccount() { Index = -1 } })
                .Where(NormalAccounts)
                .Max(a => a.Index) + 1;
        }

        /// <summary>
        /// Adds an account to the current wallet.
        /// </summary>
        /// <remarks>
        /// The name given to the account is of the form "account (i)" by default, where (i) is an incremental index starting at 0.
        /// According to BIP44, an account at index (i) can only be created when the account at index (i - 1) contains at least one transaction.
        /// </remarks>
        /// <seealso cref="https://github.com/bitcoin/bips/blob/master/bip-0044.mediawiki"/>
        /// <param name="extPubKey">The extended public key for the wallet<see cref="EncryptedSeed"/>.</param>
        /// <param name="accountIndex">Zero-based index of the account to add.</param>
        /// <param name="accountName">The name of the account.</param>
        /// <param name="accountCreationTime">Creation time of the account to be created.</param>
        /// <param name="addressCounts">The number of external and change addresses to create.</param>
        /// <returns>A new HD account.</returns>
        public HdAccount AddNewAccount(ExtPubKey extPubKey, int? accountIndex = null, string accountName = null, DateTimeOffset? accountCreationTime = null, (int external, int change)? addressCounts = null)
        {
            WalletAccounts walletAccounts = this.AccountsRoot.First().Accounts;

            accountIndex = accountIndex ?? this.NextAccountIndex();
            accountName = accountName ?? string.Format(AccountNamePattern, (int)accountIndex);
            accountCreationTime = accountCreationTime ?? DateTimeOffset.UtcNow;

            HdAccount account;

            if (this.WalletRepository == null)
            {
                account = new HdAccount
                {
                    CreationTime = accountCreationTime ?? DateTimeOffset.UtcNow,
                    Name = accountName,
                    ExtendedPubKey = extPubKey?.ToString(),
                    Index = (int)accountIndex
                };

                this.AccountsRoot.First().Accounts.Add(account);

                // TODO: Align this fully with repo behavior.
            }
            else
            {
                int defaultAddressCount = this.WalletManager?.GetAddressBufferSize() ?? 20;

                account = this.WalletRepository.CreateAccount(this.Name, (int)accountIndex, accountName, extPubKey, accountCreationTime,
                    addressCounts ?? (defaultAddressCount, defaultAddressCount));
            }

            account.WalletAccounts = walletAccounts;

            return account;
        }

        /// <summary>
        /// Gets the first account that contains no transaction.
        /// </summary>
        /// <returns>An unused account.</returns>
        public HdAccount GetFirstUnusedAccount()
        {
            // Get the accounts root for this type of coin.
            AccountRoot accountsRoot = this.AccountsRoot.Single();

            if (accountsRoot.Accounts.Any())
            {
                // Get an unused account.
                HdAccount firstUnusedAccount = accountsRoot.GetFirstUnusedAccount();
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
        public bool ContainsAddress(HdAddress address)
        {
            if (!this.AccountsRoot.Any(r => r.Accounts.Any(
                a => a.ExternalAddresses.Any(i => i.Address == address.Address) ||
                     a.InternalAddresses.Any(i => i.Address == address.Address))))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the extended private key for the given address.
        /// </summary>
        /// <param name="password">The password used to encrypt/decrypt sensitive info.</param>
        /// <param name="address">The address to get the private key for.</param>
        /// <returns>The extended private key.</returns>
        [NoTrace]
        public ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(address, nameof(address));

            // Check if the wallet contains the address.
            if (!this.ContainsAddress(address))
            {
                throw new WalletException("Address not found on wallet.");
            }

            // get extended private key
            Key privateKey = HdOperations.DecryptSeed(this.EncryptedSeed, password, this.Network);
            return HdOperations.GetExtendedPrivateKey(privateKey, this.ChainCode, address.HdPath, this.Network);
        }

        /// <summary>
        /// Lists all spendable transactions from all accounts in the wallet.
        /// </summary>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>A collection of spendable outputs.</returns>
        public IEnumerable<UnspentOutputReference> GetAllSpendableTransactions(int currentChainHeight, int confirmations = 0, Func<HdAccount, bool> accountFilter = null)
        {
            IEnumerable<HdAccount> accounts = this.GetAccounts(accountFilter);

            return accounts.SelectMany(x => x.GetSpendableTransactions(currentChainHeight, this.Network.Consensus.CoinbaseMaturity, confirmations));
        }

        /// <summary>
        /// Lists all unspent transactions from all accounts in the wallet.
        /// This is distinct from the list of spendable transactions. A transaction can be unspent but not yet spendable due to coinbase/stake maturity, for example.
        /// </summary>
        /// <param name="currentChainHeight">Height of the current chain, used in calculating the number of confirmations.</param>
        /// <param name="confirmations">The number of confirmations required to consider a transaction spendable.</param>
        /// <param name="accountFilter">An optional filter for filtering the accounts being returned.</param>
        /// <returns>A collection of spendable outputs.</returns>
        public IEnumerable<UnspentOutputReference> GetAllUnspentTransactions(int currentChainHeight, int confirmations = 0, Func<HdAccount, bool> accountFilter = null)
        {
            IEnumerable<HdAccount> accounts = this.GetAccounts(accountFilter);

            // The logic for retrieving unspent transactions is almost identical to determining spendable transactions, we just don't take coinbase/stake maturity into consideration.
            return accounts.SelectMany(x => x.GetSpendableTransactions(currentChainHeight, 0, confirmations));
        }
    }
}
