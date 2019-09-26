using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class AddressCollection : ICollection<HdAddress>
    {
        [JsonIgnore]
        public HdAccount Account;

        [JsonIgnore]
        public int AddressType;

        private List<HdAddress> addresses;
        private Wallet wallet => this.Account?.AccountRoot?.Wallet;
        private IWalletRepository repository => this.wallet?.WalletRepository;

        public int Count => this.GetAddresses().Count();
        public bool IsReadOnly => true;
        public string HdPath => (this.Account == null) ? null : $"{this.Account.HdPath}/{this.AddressType}";

        private IEnumerable<HdAddress> GetAddresses()
        {
            var addresses = (this.repository == null) ? this.addresses : this.repository.GetAccountAddresses(
                new WalletAccountReference() { WalletName = this.Account.AccountRoot.Wallet.Name, AccountName = this.Account.Name },
                this.AddressType,
                int.MaxValue);

            foreach (HdAddress address in addresses)
            {
                address.AddressCollection = this;
                yield return address;
            }
        }

        public AddressCollection(ICollection<HdAddress> addresses)
        {
        }

        public AddressCollection(HdAccount account, int addressType, ICollection<HdAddress> addresses)
        {
            this.Account = account;
            this.AddressType = addressType;

            foreach (HdAddress address in addresses)
                address.AddressCollection = this;

            this.addresses = addresses.ToList();
        }

        public AddressCollection(HdAccount account, int addressType)
            : this(account, addressType, new List<HdAddress>())
        {
        }

        public void Add(HdAddress address)
        {
            if (address.AddressType == null)
                address.AddressType = this.AddressType;

            if (address.AddressType != this.AddressType)
                throw new InvalidOperationException("Conflicting address type detected.");

            if (this.repository == null)
                this.addresses.Add(address);
            else
                this.repository.AddWatchOnlyAddresses(this.wallet.Name, this.Account.Name, this.AddressType, new List<HdAddress>() { address });

            address.AddressCollection = this;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(HdAddress account)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(HdAddress[] arr, int index)
        {
            foreach (HdAddress address in this.GetAddresses())
                arr[index++] = address;
        }

        public bool Remove(HdAddress account)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<HdAddress> GetEnumerator()
        {
            return GetAddresses().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    /// <summary>
    /// An HD account's details.
    /// </summary>
    public class HdAccount
    {
        [JsonIgnore]
        public WalletAccounts WalletAccounts { get; set; }

        [JsonIgnore]
        public AccountRoot AccountRoot => this.WalletAccounts?.AccountRoot;

        [JsonConstructor]
        public HdAccount(ICollection<HdAddress> externalAddresses, ICollection<HdAddress> internalAddresses)
        {
            this.ExternalAddresses = new AddressCollection(this, 0, externalAddresses);
            this.InternalAddresses = new AddressCollection(this, 1, internalAddresses);
        }

        public HdAccount()
        {
            this.ExternalAddresses = new AddressCollection(this, 0);
            this.InternalAddresses = new AddressCollection(this, 1);
        }

        public HdAccount(WalletAccounts walletAccounts)
            : this()
        {
            this.WalletAccounts = walletAccounts;
            walletAccounts.Add(this);
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

        [JsonIgnore]
        private string hdPath { get; set; }

        /// <summary>
        /// A path to the account as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath {
            get
            {
                return (this.WalletAccounts != null) ? $"{this.WalletAccounts.HdPath}/{this.Index}'" : this.hdPath;
            }

            set
            {
                this.hdPath = value;
            }
        }

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
        public AddressCollection ExternalAddresses { get; set; }

        /// <summary>
        /// The list of internal addresses, typically used to receive change.
        /// </summary>
        [JsonProperty(PropertyName = "internalAddresses")]
        public AddressCollection InternalAddresses { get; set; }

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
        /// Gets unused addresses.
        /// </summary>
        /// <param name="count">The number of unused addresses to get.</param>
        /// <param name="isChange">Set to <c>true</c> to get change addresses.</param>
        /// <returns>An enumeration of unused addresses.</returns>
        public IEnumerable<HdAddress> GetUnusedAddresses(int count, bool isChange = false)
        {
            var accountReference = new WalletAccountReference(this.AccountRoot.Wallet.Name, this.Name);

            foreach (HdAddress address in this.WalletAccounts.AccountRoot.Wallet.WalletRepository.GetUnusedAddresses(accountReference, count, isChange))
            {
                address.AddressCollection = (AddressCollection)(isChange ? this.InternalAddresses : this.ExternalAddresses);

                yield return address;
            }
        }

        /*
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
                var newAddress = new HdAddress()
                {
                    Index = i,
                    HdPath = HdOperations.CreateHdPath((int)this.GetCoinType(), this.Index, isChange, i),
                    ScriptPubKey = address.ScriptPubKey,
                    Pubkey = pubkey.ScriptPubKey,
                    Address = address.ToString()
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
        */
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
}