using System;
using System.Collections;
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
    public class TransactionCollection : ICollection<TransactionData>
    {
        private ICollection<TransactionData> transactions;
        public HdAddress Address { get; set; }
        private HdAccount account => this.Address?.AddressCollection?.Account;
        private Wallet wallet => this.account?.AccountRoot?.Wallet;
        private IWalletRepository repository => this.wallet?.WalletRepository;

        public int Count => this.GetTransactions().Count();
        public bool IsReadOnly => true;

        public AddressIdentifier GetAddressIdentifier()
        {
            return this.repository.GetAddressIdentifier(this.wallet.Name, this.account.Name, this.Address.AddressCollection.AddressType, this.Address.Index);
        }

        private IEnumerable<TransactionData> GetTransactions()
        {
            // TODO: if (this.transactions == null)
            {
                if (this.repository == null)
                    this.transactions = this.transactions ?? new List<TransactionData>();
                else
                    this.transactions = this.repository.GetAllTransactions(this.Address).ToList();
            }

            return this.transactions;
        }

        [JsonConstructor]
        public TransactionCollection(ICollection<TransactionData> transactions)
            : this(null, transactions)
        {
        }

        public TransactionCollection(HdAddress address, ICollection<TransactionData> transactions)
        {
            this.Address = address;

            if (this.repository == null)
            {
                this.transactions = transactions;
                return;
            }

            if (this.repository != null && transactions != null)
                foreach (TransactionData transaction in transactions)
                    this.Add(transaction);
        }

        public TransactionCollection(HdAddress address)
            : this(address, new List<TransactionData>())
        {
        }

        public void Add(TransactionData transaction)
        {
            if (transaction.ScriptPubKey == null)
                transaction.ScriptPubKey = this.Address.ScriptPubKey;

            if (this.repository == null)
                this.transactions.Add(transaction);
            else
                this.repository.AddWatchOnlyTransactions(this.wallet.Name, this.account.Name, this.Address, new[] { transaction });
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(TransactionData transactionData)
        {
            return this.GetTransactions().Any(t => t.Id == transactionData.Id && t.Index == transactionData.Index && t.ScriptPubKey.ToHex() == transactionData.ScriptPubKey.ToHex());
        }

        public void CopyTo(TransactionData[] arr, int index)
        {
            foreach (TransactionData transaction in this.GetTransactions())
                arr[index++] = transaction;
        }

        public bool Remove(TransactionData transactionData)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<TransactionData> GetEnumerator()
        {
            return GetTransactions().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    /// <summary>
    /// An HD address.
    /// </summary>
    public class HdAddress
    {
        [JsonIgnore]
        public AddressCollection AddressCollection;

        [JsonConstructor]
        public HdAddress(ICollection<TransactionData> transactions = null)
        {
            this.Transactions = (transactions == null ) ? new TransactionCollection(this) : new TransactionCollection(this, transactions);
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

        [JsonIgnore]
        private string hdPath;

        /// <summary>
        /// A path to the address as defined in BIP44.
        /// </summary>
        [JsonProperty(PropertyName = "hdPath")]
        public string HdPath
        {
            get
            {
                if (this.AddressCollection == null)
                    return this.hdPath;

                return $"{this.AddressCollection.HdPath}/{this.Index}";
            }

            set
            {
                this.hdPath = value;

                if (!string.IsNullOrEmpty(this.hdPath))
                {
                    // "m/44'/105'/0'/0/0"
                    string[] parts = this.hdPath.Split('/');

                    if (parts.Length < 5)
                        throw new FormatException($"The supplied '{nameof(this.HdPath)}' is too short.");

                    this.addressType = int.Parse(parts[4]);

                    if (parts.Length > 5)
                        this.Index = int.Parse(parts[5]);
                }
            }
        }

        /// <summary>
        /// A list of transactions involving this address.
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public TransactionCollection Transactions { get; set; }

        /// <summary>
        /// Determines whether this is a change address or a receive address.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if it is a change address; otherwise, <c>false</c>.
        /// </returns>
        [NoTrace]
        public bool IsChangeAddress()
        {
            Guard.NotNull(this.addressType, nameof(this.addressType));

            return this.AddressType == 1;
        }

        [JsonIgnore]
        private int? addressType;

        [NoTrace]
        [JsonIgnore]
        public int? AddressType
        {
            get
            {
                if (this.AddressCollection != null)
                    return this.AddressCollection.AddressType;

                return this.addressType;
            }

            set
            {
                this.addressType = value;

                if (this.addressType == null)
                {
                    this.hdPath = null;
                }
                else if (!string.IsNullOrEmpty(this.hdPath))
                {
                    // "m/44'/105'/0'/0/0"
                    string[] parts = this.hdPath.Split('/');
                    parts[4] = this.addressType.ToString();
                    this.hdPath = string.Join("/", parts);
                }
            }
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

        public static (Money confirmedAmount, Money unConfirmedAmount) GetBalances(IEnumerable<TransactionData> transactions)
        {
            List<TransactionData> allTransactions = transactions.ToList();

            long confirmed = allTransactions.Sum(t => t.GetUnspentAmount(true));
            long total = allTransactions.Sum(t => t.GetUnspentAmount(false));

            return (confirmed, total - confirmed);
        }

        /// <summary>
        /// Get the address total spendable value for both confirmed and unconfirmed UTXO.
        /// </summary>
        public (Money confirmedAmount, Money unConfirmedAmount) GetBalances()
        {
            return GetBalances(this.Transactions);
        }
    }
}