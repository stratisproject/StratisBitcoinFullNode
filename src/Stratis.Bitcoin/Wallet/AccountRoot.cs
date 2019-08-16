using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;

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
}